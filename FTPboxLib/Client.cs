/* License
 * This file is part of FTPbox - Copyright (C) 2012-2013 ftpbox.org
 * FTPbox is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published 
 * by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed 
 * in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>.
 */
/* Client.cs
 * The client class handles communication with the server, combining the FTP and SFTP libraries.
 */

// #define __MonoCs__

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Collections.Generic;
using System.IO;
#if !__MonoCs__
using FileIO = Microsoft.VisualBasic.FileIO;
#endif

namespace FTPboxLib
{    
	public class Client
    {
        public Client(AccountController account)
        {
            this.controller = account;
            this.Certificates = new X509Certificate2Collection();
        }

        #region Private Fields

        private FtpClient _ftpc;             // Our FTP client
        private SftpClient _sftpc;           // And our SFTP client

        private bool _reconnecting;          // true when client is already attempting to reconnect

	    private X509Certificate2Collection Certificates;

	    private Timer tKeepAlive;

	    private AccountController controller;

        #endregion

        #region Public Events

        public event EventHandler DownloadComplete;
        public event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;
        public event EventHandler ReconnectingFailed;
        public event EventHandler<ValidateCertificateEventArgs> ValidateCertificate;
        public event EventHandler<TransferProgressArgs> TransferProgress;

        #endregion

        #region Methods

        /// <summary>
        /// Connect to the remote servers, with the details from Profile
        /// </summary>
        /// <param name="reconnecting">True if this is an attempt to re-establish a closed connection</param>
        public void Connect(bool reconnecting = false)
        {
            Notifications.ChangeTrayText(reconnecting ? MessageType.Reconnecting : MessageType.Connecting);
            Log.Write(l.Debug, "{0} client...", reconnecting ? "Reconnecting" : "Connecting");
            
			if (FTP)
			{
			    _ftpc = new FtpClient { Host = controller.Account.Host, Port = controller.Account.Port };

                // Add accepted certificates
                _ftpc.ClientCertificates.AddRange(Certificates);
                
                if (controller.Account.Protocol == FtpProtocol.FTPS)
                {
                    _ftpc.ValidateCertificate += (sender, x) =>
                    {
                        var fingerPrint = new X509Certificate2(x.Certificate).Thumbprint;
                        
                        if (_ftpc.ClientCertificates.Count <= 0 && x.PolicyErrors != SslPolicyErrors.None)
                        {
                            Certificates.Add(x.Certificate);
                            x.Accept = false;
                            return;
                        }

                        // if ValidateCertificate handler isn't set, accept the certificate and move on
                        if (ValidateCertificate == null || Settings.TrustedCertificates.Contains(fingerPrint))
                        {
                            Log.Write(l.Client, "Trusted: {0}", fingerPrint);
                            x.Accept = true;
                            return;
                        }

                        var e = new ValidateCertificateEventArgs
                        {
                            Fingerprint = fingerPrint,
                            SerialNumber = x.Certificate.GetSerialNumberString(),
                            Algorithm = x.Certificate.GetKeyAlgorithmParametersString(),
                            ValidFrom = x.Certificate.GetEffectiveDateString(),
                            ValidTo = x.Certificate.GetExpirationDateString(),
                            Issuer = x.Certificate.Issuer
                        };
                        // Prompt user to validate
                        ValidateCertificate(null, e);
                        x.Accept = e.IsTrusted;
                    };
                    
                    // Change Security Protocol
                    if (controller.Account.FtpsMethod == FtpsMethod.Explicit)
                        _ftpc.EncryptionMode = FtpEncryptionMode.Explicit;
                    else if (controller.Account.FtpsMethod == FtpsMethod.Implicit)
                        _ftpc.EncryptionMode = FtpEncryptionMode.Implicit;
                }

                _ftpc.Credentials = new NetworkCredential(controller.Account.Username, controller.Account.Password);			    

                try
                {
                    _ftpc.Connect();
                }
                catch (Exception exc)
                {
                    // Since the ClientCertificates are added when accepted in ValidateCertificate, the first 
                    // attempt to connect will fail with an AuthenticationException. If this is the case, a 
                    // re-connect is attempted, this time with the certificates properly set.
                    // This is a workaround to avoid storing Certificate files locally...
                    if (exc is System.Security.Authentication.AuthenticationException
                        && _ftpc.ClientCertificates.Count <= 0)
                        Connect();
                    else
                        throw;
                }
			}
			else // SFTP
			{
			    ConnectionInfo connectionInfo;
                if (controller.isPrivateKeyValid)
                    connectionInfo = new PrivateKeyConnectionInfo(controller.Account.Host, controller.Account.Port, controller.Account.Username, new PrivateKeyFile(controller.Account.PrivateKeyFile, controller.Account.Password));
                else
                    connectionInfo = new PasswordConnectionInfo(controller.Account.Host, controller.Account.Port, controller.Account.Username, controller.Account.Password);
                
                _sftpc = new SftpClient(connectionInfo);
                _sftpc.ConnectionInfo.AuthenticationBanner += (o, x) => Log.Write(l.Warning, x.BannerMessage);			   

                _sftpc.HostKeyReceived += (o, x) =>
                {
                    var fingerPrint = x.FingerPrint.GetCertificateData();

                    // if ValidateCertificate handler isn't set, accept the certificate and move on
                    if (ValidateCertificate == null || Settings.TrustedCertificates.Contains(fingerPrint))
                    {
                        Log.Write(l.Client, "Trusted: {0}", fingerPrint);
                        x.CanTrust = true; 
                        return;
                    }

                    var e = new ValidateCertificateEventArgs
                    {
                        Fingerprint = fingerPrint,
                        Key = x.HostKeyName,
                        KeySize = x.KeyLength.ToString()
                    };
                    // Prompt user to validate
                    ValidateCertificate(null, e);
                    x.CanTrust = e.IsTrusted;
                };
                
                _sftpc.Connect();			    
                
                _sftpc.ErrorOccurred += (o, e) =>
                {
                    if (!isConnected) Notifications.ChangeTrayText(MessageType.Nothing);
                    if (ConnectionClosed != null) ConnectionClosed(null, new ConnectionClosedEventArgs { Text = e.Exception.Message });

                    if (e.Exception is Renci.SshNet.Common.SftpPermissionDeniedException)
                        Log.Write(l.Warning, "Permission denied error occured");
                    if (e.Exception is Renci.SshNet.Common.SshConnectionException)
                        Reconnect();         
                };
			}

            controller.HomePath = WorkingDirectory;
            
            if (isConnected)
                if (!string.IsNullOrWhiteSpace(controller.Paths.Remote) && !controller.Paths.Remote.Equals("/"))
                    WorkingDirectory = controller.Paths.Remote;

            Log.Write(l.Debug, "Client connected sucessfully");
            Notifications.ChangeTrayText(MessageType.Ready);

            if (Settings.IsDebugMode) 
                LogServerInfo();
            
            // Periodically send NOOP (KeepAlive) to server if a non-zero interval is set            
            SetKeepAlive();
        }
	    
        /// <summary>
        /// Attempt to reconnect to the server. Called when connection has closed.
        /// </summary>
        public void Reconnect()
        {
            if (_reconnecting) return;            
            try
            {
                _reconnecting = true;
                Connect();
            }
            catch (Exception ex)
            {
                Common.LogError(ex);
                Notifications.ChangeTrayText(MessageType.Disconnected);
                ReconnectingFailed.SafeInvoke(null, EventArgs.Empty);
            }
            finally
            {
                _reconnecting = false;
            }
        }

        /// <summary>
        /// Close connection to server
        /// </summary>
        public void Disconnect()
        {
            if (FTP)
                _ftpc.Disconnect();
            else
                _sftpc.Disconnect();
        }

        /// <summary>
        /// Keep the connection to the server alive by sending the NOOP command
        /// </summary>
        private void SendNoOp()
        {
            if (controller.SyncQueue.Running) return;

            try
            {
                Console.WriteLine("NOOP");
                if (FTP)
                    _ftpc.Execute("NOOP");
                else
                    _sftpc.SendKeepAlive();
            }
            catch (Exception ex)
            {
                Common.LogError(ex);
                Reconnect();
            }
        }

        /// <summary>
        /// Set a timer that will periodically send the NOOP
        /// command to the server if a non-zero interval is set
        /// </summary>
        public void SetKeepAlive()
        {
            // Dispose the existing timer
            UnsetKeepAlive();

            if (tKeepAlive == null) tKeepAlive = new Timer(state => SendNoOp());

            if (controller.Account.KeepAliveInterval > 0)
                tKeepAlive.Change(1000 * 10, 1000 * controller.Account.KeepAliveInterval);
        }

        /// <summary>
        /// Dispose the existing KeepAlive timer
        /// </summary>
	    public void UnsetKeepAlive()
	    {
            if (tKeepAlive != null) tKeepAlive.Change(0,0);
	    }

        public void Upload(string localpath, string remotepath)
        {
            if (FTP)
                using (Stream file = File.OpenRead(localpath), 
                               rem = _ftpc.OpenWrite(remotepath))
                {
                    var buf = new byte[8192];
                    int read;
                    long total = 0;


                    while ((read = file.Read(buf, 0, buf.Length)) > 0)
                    {
                        rem.Write(buf, 0, read);
                        total += read;

                        Console.WriteLine("{0}/{1} {2:p}",
                            total, file.Length,
                            (double)total / (double)file.Length);
                    }
                }
            else
                using (var file = File.OpenRead(localpath))
                    _sftpc.UploadFile(file, remotepath, true);
        }

        /// <summary>
        /// Upload to a temporary file. 
        /// If the transfer is successful, replace the old file with the temporary one.
        /// If not, delete the temporary file.
        /// </summary>
        /// <param name="i">The item to upload</param>
        /// <returns>TransferStatus.Success on success, TransferStatus.Success on failure</returns>
        public TransferStatus SafeUpload(SyncQueueItem i)
        {
            // is this the first time we check the files?
            if (controller.FileLog.isEmpty())
            {
                //TODO: allow user to select if the following should happen
                // skip synchronization if the file already exists and has the exact same size
                if (this.Exists(i.CommonPath) && SizeOf(i.CommonPath) == i.Item.Size)
                {
                    Log.Write(l.Client, "File seems to be already synced (skipping): {0}", i.CommonPath);
                    return TransferStatus.Success;
                }
            }

            Notifications.ChangeTrayText(MessageType.Uploading, i.Item.Name);
            string temp = Common._tempName(i.CommonPath, controller.Account.TempFilePrefix);

            try
            {
                var _startedOn = DateTime.Now;
                long _transfered = 0;
                // upload to a temp file...
                if (FTP)
                {
                    using (Stream file = File.Open(i.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), rem = _ftpc.OpenWrite(temp))
                    {
                        var buf = new byte[8192];
                        int read;

                        while ((read = file.Read(buf, 0, buf.Length)) > 0)
                        {
                            rem.Write(buf, 0, read);
                            _transfered += read;

                            ReportTransferProgress(new TransferProgressArgs(read, _transfered, i, _startedOn));

                            ThrottleTransfer(Settings.General.UploadLimit, _transfered, _startedOn);
                        }
                    }
                }
                else
                    using (var file = File.Open(i.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        _sftpc.UploadFile(file, temp, true,
                            (d) => 
                                {
                                    ReportTransferProgress(new TransferProgressArgs((long) d-_transfered, (long) d, i, _startedOn));
                                    _transfered = (long)d;
                                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex);
                if (FTP) CheckWorkingDirectory();
                return TransferStatus.Failure;
            }

            if (i.Item.Size == SizeOf(temp))
            {
                if (Exists(i.CommonPath)) Remove(i.CommonPath);
                Rename(temp, i.CommonPath);

                return TransferStatus.Success;
            }
            else
            {
                Remove(temp);
                return TransferStatus.Failure;
            }
        }

        public void Download(string cpath, string lpath)
        {
            if (FTP)
            {
                using (Stream file = File.OpenWrite(lpath), rem = _ftpc.OpenRead(cpath))
                {
                    var buf = new byte[8192];
                    int read;

                    while ((read = rem.Read(buf, 0, buf.Length)) > 0)
                        file.Write(buf, 0, read);
                }
            }
            else
                using (var f = new FileStream(lpath, FileMode.Create, FileAccess.ReadWrite))
                    _sftpc.DownloadFile(cpath, f);
        }

        /// <summary>
        /// Download to a temporary file. 
        /// If the transfer is successful, replace the old file with the temporary one.
        /// If not, delete the temporary file.
        /// </summary>
        /// <param name="i">The item to download</param>
        /// <returns>TransferStatus.Success on success, TransferStatus.Success on failure</returns>
        public TransferStatus SafeDownload(SyncQueueItem i)
        {
            Notifications.ChangeTrayText(MessageType.Downloading, i.Item.Name);
            string temp = Common._tempLocal(i.LocalPath, controller.Account.TempFilePrefix);
            try
            {
                var _startedOn = DateTime.Now;
                long _transfered = 0;
                // download to a temp file...
                if (FTP)
                {
                    using (Stream file = File.OpenWrite(temp), rem = _ftpc.OpenRead(i.CommonPath))
                    {
                        var buf = new byte[8192];
                        int read;                        

                        while ((read = rem.Read(buf, 0, buf.Length)) > 0)
                        {
                            file.Write(buf, 0, read);
                            _transfered += read;
                            
                            ReportTransferProgress(new TransferProgressArgs(read, _transfered, i, _startedOn));

                            ThrottleTransfer(Settings.General.DownloadLimit, _transfered, _startedOn);
                        }
                    }
                }
                else
                    using (var f = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite))
                        _sftpc.DownloadFile(i.CommonPath, f,
                            (d) =>
                                {                                    
                                    ReportTransferProgress(new TransferProgressArgs((long) d-_transfered, (long) d, i, _startedOn));
                                    _transfered = (long)d;
                                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex);
                if (FTP) CheckWorkingDirectory();
                goto Finish;
            }

            if (i.Item.Size == new FileInfo(temp).Length)
            {
                controller.FolderWatcher.Pause();   // Pause Watchers
                if (File.Exists(i.LocalPath)) 
                    #if __MonoCs__
                    File.Delete(i.LocalPath);
                    #else
                    FileIO.FileSystem.DeleteFile(i.LocalPath, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin);
                    #endif
                File.Move(temp, i.LocalPath);
                controller.FolderWatcher.Resume();  // Resume Watchers
                return TransferStatus.Success;
            }

        Finish:
            if (File.Exists(temp))
                #if __MonoCs__
                File.Delete(temp);
                #else
                FileIO.FileSystem.DeleteFile(temp, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin);
                #endif
            return TransferStatus.Failure;
        }

	    public void Rename(string oldname, string newname)
        {
            if (FTP)
                _ftpc.Rename(oldname, newname);
            else
                _sftpc.RenameFile(oldname, newname);
        }

        public void MakeFolder(string cpath)
        {
            try
            {
                if (FTP)
                    _ftpc.CreateDirectory(cpath);
                else
                    _sftpc.CreateDirectory(cpath);
            }
            catch
            {
                if (!Exists(cpath)) throw;
            }
        }

        /// <summary>
        /// Delete a file
        /// </summary>
        /// <param name="cpath">Path to the file</param>
        public void Remove(string cpath)
        {
            if (FTP)
                _ftpc.DeleteFile(cpath);
            else
                _sftpc.Delete(cpath);
        }

        /// <summary>
        /// Delete a remote folder and everything inside it
        /// </summary>
        /// <param name="path">Path to folder that will be deleted</param>
        /// <param name="skipIgnored">if true, files that are normally ignored will not be deleted</param>
        public void RemoveFolder(string path, bool skipIgnored = true)
        {
            if (!Exists(path)) return;

            Log.Write(l.Client, "About to delete: {0}", path);
            // Empty the folder before deleting it
            // List is reversed to delete an files before their parent folders
            foreach (var i in ListRecursive(path, skipIgnored).Reverse())
            {
                Console.Write("\r Removing: {0,50}", i.FullPath);
                if (i.Type == ClientItemType.File)
                    Remove(i.FullPath);
                else
                {
                    if (FTP)
                        _ftpc.DeleteDirectory(i.FullPath);
                    else
                        _sftpc.DeleteDirectory(i.FullPath);
                }
            }

            if (FTP)
                _ftpc.DeleteDirectory(path);
            else
                _sftpc.DeleteDirectory(path);

            Log.Write(l.Client, "Deleted: {0}", path);
        }

        /// <summary>
        /// Make sure that our client's working directory is set to the user-selected Remote Path.
        /// If a previous operation failed and the working directory wasn't properly restored, this will prevent further issues.
        /// </summary>
        /// <returns>false if changing to RemotePath fails, true in any other case</returns>
        public bool CheckWorkingDirectory()
        {
            try
            {
                string cd = WorkingDirectory;
                if (cd != controller.Paths.Remote)
                {
                    Log.Write(l.Warning, "pwd is: {0} should be: {1}", cd, controller.Paths.Remote);
                    WorkingDirectory = controller.Paths.Remote;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (!isConnected) Log.Write(l.Warning, "Client not connected!");
                Common.LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// Throttle the file transfer if speed limits apply.
        /// </summary>
        /// <param name="limit">The download or upload rate to limit to, in kB/s.</param>
        /// <param name="transfered">bytes already transferred.</param>
        /// <param name="startedOn">when did the transfer start.</param>
        private void ThrottleTransfer(int limit, long transfered, DateTime startedOn)
        {
            var elapsed = DateTime.Now.Subtract(startedOn);
            var rate = (int)(elapsed.TotalSeconds < 1 ? transfered : transfered / elapsed.TotalSeconds);
            if (limit > 0 && rate > 1000 * limit)
            {
                double millisecDelay = (transfered / limit - elapsed.Milliseconds);

                if (millisecDelay > Int32.MaxValue)
                    millisecDelay = Int32.MaxValue;

                Thread.Sleep((int)millisecDelay);
            }
        }

        /// <summary>
        /// Displays some server info in the log/console
        /// </summary>
        public void LogServerInfo()
        {
            Log.Write(l.Client, "////////////////////Server Info///////////////////");         
            if (!FTP)
            {
                Log.Write(l.Client, "Protocol Version: {0}", _sftpc.ProtocolVersion);
                Log.Write(l.Client, "Client Compression Algorithm: {0}", _sftpc.ConnectionInfo.CurrentClientCompressionAlgorithm);
                Log.Write(l.Client, "Server Compression Algorithm: {0}", _sftpc.ConnectionInfo.CurrentServerCompressionAlgorithm);
                Log.Write(l.Client, "Client encryption: {0}", _sftpc.ConnectionInfo.CurrentClientEncryption);
                Log.Write(l.Client, "Server encryption: {0}", _sftpc.ConnectionInfo.CurrentServerEncryption);
            }
            else
            {
                Log.Write(l.Client, "System type: {0}", _ftpc.SystemType);
                Log.Write(l.Client, "Encryption Mode: {0}", _ftpc.EncryptionMode);                
                Log.Write(l.Client, "Character Encoding: {0}", _ftpc.Encoding);
            }

            Log.Write(l.Client, "//////////////////////////////////////////////////");
        }

        /// <summary>
        /// Safely invoke TransferProgress.
        /// </summary>
        private void ReportTransferProgress(TransferProgressArgs e)
        {
            if (TransferProgress != null)
                TransferProgress(null, e);
        }

        /// <summary>
        /// Returns the file size of the file in the given bath, in both SFTP and FTP
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The file's size</returns>
        public long SizeOf(string path)
        {
            return (FTP) ? _ftpc.GetFileSize(path) : _sftpc.GetAttributes(path).Size;
        }

        /// <summary>
        /// Does the specified path exist on the remote folder?
        /// </summary>
        public bool Exists(string cpath)
        {
            if (FTP)
                return _ftpc.FileExists(cpath) || _ftpc.DirectoryExists(cpath);
            else
                return _sftpc.Exists(cpath);
        }

        /// <summary>
        /// Returns the LastWriteTime of the specified file/folder
        /// </summary>
        /// <param name="path">The common path to the file/folder</param>
        /// <returns></returns>
        public DateTime GetLwtOf(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return DateTime.MinValue;

            if (path.StartsWith("/")) path = path.Substring(1);            
            var dt = DateTime.MinValue;

            try
            {
                dt = (FTP) ? _ftpc.GetModifiedTime(path) : _sftpc.GetLastWriteTime(path);
            }
            catch (Exception ex)
            {
                Log.Write(l.Client, "===> {0} is a folder", path);
                Common.LogError(ex);
            }

            if (!FTP)
                Log.Write(l.Client, "Got LWT: {0} UTC: {1}", dt, _sftpc.GetLastAccessTimeUtc(path));

            return dt;
        }

        /// <summary>
        /// Convert SftpFile to ClientItemType
        /// </summary>
        private ClientItemType _ItemTypeOf(SftpFile f)
        {
            if (f.IsDirectory)
                return ClientItemType.Folder;
            if (f.IsRegularFile)
                return ClientItemType.File;
            return ClientItemType.Other;
        }

        /// <summary>
        /// Convert FtpFileSystemObjectType to ClientItemType
        /// </summary>
        private ClientItemType _ItemTypeOf(FtpFileSystemObjectType f)
        {
            if (f == FtpFileSystemObjectType.File)
                return ClientItemType.File;
            if (f == FtpFileSystemObjectType.Directory)
                return ClientItemType.Folder;
            return ClientItemType.Other;
        }

        #endregion                

        #region Properties

        private bool FTP
        {
            get { return (controller.Account.Protocol != FtpProtocol.SFTP); }
        }

        public bool isConnected
        {
            get
            {
                return (FTP) ? _ftpc.IsConnected : _sftpc.IsConnected;
            }
        }

        public bool ListingFailed { get; private set; }

        public string WorkingDirectory
		{
			get
			{
                return (FTP) ? _ftpc.GetWorkingDirectory() : _sftpc.WorkingDirectory;
			}
			set 
			{
				if (FTP)
					_ftpc.SetWorkingDirectory(value);
				else
					_sftpc.ChangeDirectory(value);
                Log.Write(l.Client, "cd {0}", value);
			}
        }

	    #endregion

        #region Listing

        /// <summary>
        /// Returns a non-recursive list of files/folders inside the specified path       
        /// </summary>
        /// <param name="cpath">path to folder to list inside</param>
        /// <param name="skipIgnored">if true, ignored items are not returned</param>
        public IEnumerable<ClientItem> List(string cpath, bool skipIgnored = true)
        {
            ListingFailed = false;
            UnsetKeepAlive();

            var list = new List<ClientItem>();

            try
            {
                if (FTP)
                    list = Array.ConvertAll(new List<FtpListItem>(_ftpc.GetListing(cpath)).ToArray(), ConvertItem).ToList();
                else
                    list = Array.ConvertAll(new List<SftpFile>(_sftpc.ListDirectory(cpath)).ToArray(), ConvertItem).ToList();
            }
            catch(Exception ex)
            {
                Common.LogError(ex);
                ListingFailed = true;
                yield break;
            }

            list.RemoveAll(x => x.Name == "." || x.Name == "..");
            if (skipIgnored)
                list.RemoveAll(x => x.FullPath.Contains("webint"));

            foreach (var f in list.Where(x => x.Type != ClientItemType.Other))
                yield return f;

            SetKeepAlive();
        }

        /// <summary>
        /// Get a full list of files/folders inside the specified path
        /// </summary>
        /// <param name="cpath">path to folder to list inside</param>
        /// <param name="skipIgnored">if true, ignored items are not returned</param>
        public IEnumerable<ClientItem> ListRecursive(string cpath, bool skipIgnored = true)
        {
            var list = new List<ClientItem>(List(cpath, skipIgnored).ToList());
            if (ListingFailed) yield break;

            if (skipIgnored)
                list.RemoveAll(x => !controller.ItemGetsSynced(x.FullPath, false));
            
            foreach (var f in list.Where(x => x.Type == ClientItemType.File)) 
                yield return f;
            
            foreach (var d in list.Where(x => x.Type == ClientItemType.Folder))
                foreach (var f in ListRecursiveInside(d, skipIgnored))
                    yield return f;
        }

        /// <summary>
        /// Returns a fully recursive listing inside the specified (directory) item
        /// </summary>
        /// <param name="p">The clientItem (should be of type directory) to list inside</param>
        /// <param name="skipIgnored">if true, ignored items are not returned</param>
        private IEnumerable<ClientItem> ListRecursiveInside(ClientItem p, bool skipIgnored = true)
	    {
	        yield return p;

            var cpath = controller.GetCommonPath(p.FullPath, false);

            var list = new List<ClientItem>(List(cpath, skipIgnored).ToList());
            if (ListingFailed) yield break;

            if (skipIgnored)
                list.RemoveAll(x => !controller.ItemGetsSynced(x.FullPath, false));

            foreach (var f in list.Where(x => x.Type == ClientItemType.File))
                yield return f;

            foreach (var d in list.Where(x => x.Type == ClientItemType.Folder))
                foreach (var f in ListRecursiveInside(d, skipIgnored))
                    yield return f;
	    }

        /// <summary>
        /// Convert an FtpItem to a ClientItem
        /// </summary>
        private ClientItem ConvertItem(FtpListItem f)
        {
            var fullPath = f.FullName;
            if (fullPath.StartsWith("./"))
            {
                var cwd = WorkingDirectory;
                var wd = (controller.Paths.Remote != null && cwd.StartsWithButNotEqual(controller.Paths.Remote) && cwd != "/") ? cwd : controller.GetCommonPath(cwd, false);
                fullPath = fullPath.Substring(2);
                if (wd != "/")
                    fullPath = string.Format("/{0}/{1}", wd, fullPath);
                fullPath = fullPath.Replace("//", "/");
            }
            
            return new ClientItem
                {
                    Name = f.Name,
                    FullPath = fullPath,
                    Type = _ItemTypeOf(f.Type),
                    Size = f.Size,
                    LastWriteTime = f.Modified
                };
        }

        /// <summary>
        /// Convert an SftpFile to a ClientItem
        /// </summary>
        private ClientItem ConvertItem(SftpFile f)
        {
            return new ClientItem
                {
                    Name = f.Name,
                    FullPath = controller.GetCommonPath(f.FullName, false),
                    Type = _ItemTypeOf(f),
                    Size = f.Attributes.Size,
                    LastWriteTime = f.LastWriteTime
                };
        }

        #endregion
    }
}