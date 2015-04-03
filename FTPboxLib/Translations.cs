/* License
 * This file is part of FTPbox - Copyright (C) 2012-2013 ftpbox.org
 * FTPbox is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published 
 * by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed 
 * in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>.
 */
/* Translations.cs
 * Manage all translations, which are loaded from the translations.xml file
 */

using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

namespace FTPboxLib
{
    public class Translations
    {
        XmlDocument xmlDocument = new XmlDocument();       
        string documentPath = Environment.CurrentDirectory + "\\translations.xml";

        public Translations()
        {
            try { xmlDocument.Load(documentPath); }
            catch (Exception ex) { Log.Write(l.Info, "?>" + ex.Message); xmlDocument.LoadXml("<translations></translations>"); }
        }

        public string this[MessageType t]
        {
            get
            {
                switch (t)
                {
                    default:
                        return null;
                    case MessageType.ItemChanged:
                        return Get("/tray/changed", "{0} han cambiado.");
                    case MessageType.ItemCreated:
                        return Get("/tray/created", "{0} han sido creados.");
                    case MessageType.ItemDeleted:
                        return Get("/tray/deleted", "{0} han sido eliminados.");
                    case MessageType.ItemRenamed:
                        return Get("/tray/renamed", "{0} ha sido renombrado a {1}.");
                    case MessageType.ItemUpdated:
                        return Get("/tray/updated", "{0} han sido actualizados.");
                    case MessageType.FilesOrFoldersUpdated:
                        return Get("/tray/FilesOrFoldersUpdated", "{0} {1} han sido actualizados");
                    case MessageType.FilesOrFoldersCreated:
                        return Get("/tray/FilesOrFoldersCreated", "{0} {1} han sido creados");
                    case MessageType.FilesAndFoldersChanged:
                        return Get("/tray/FilesAndFoldersChanged", "{0} {1} y {2} {3} han sido actualizados");
                    case MessageType.ItemsDeleted:
                        return Get("/tray/ItemsDeleted", "{0} elementos han sido eliminados.");
                    case MessageType.File:
                        return Get("/tray/file", "Archivo");
                    case MessageType.Files:
                        return Get("/tray/files", "Archivos");
                    case MessageType.Folder:
                        return Get("/tray/folder", "Carpeta");
                    case MessageType.Folders:
                        return Get("/tray/folders", "Carpetas");
                    case MessageType.LinkCopied:
                        return Get("/tray/link_copied", "Enlace copiado al portapapeles");
                    case MessageType.Connecting:
                        return Get("/tray/connecting", "Escloud - Conectando...");
                    case MessageType.Disconnected:
                        return Get("/tray/disconnected", "Escloud - Desconectado");
                    case MessageType.Reconnecting:
                        return Get("/tray/reconnecting", "Escloud - Conectando de nuevo...");
                    case MessageType.Listing:
                        return Get("/tray/listing", "Escloud - Listando...");
                    case MessageType.Uploading:
                        return Get("/tray/uploading", "Enviando {0}");
                    case MessageType.Downloading:
                        return Get("/tray/downloading", "Recibiendo {0}");
                    case MessageType.Syncing:
                        return Get("/tray/syncing", "Escloud - Sincronizando");
                    case MessageType.AllSynced:
                        return Get("/tray/synced", "Escloud - Todos los archivos sincronizados");
                    case MessageType.Offline:
                        return Get("/tray/offline", "Escloud - Fuera de línea");
                    case MessageType.Ready:
                        return Get("/tray/ready", "Escloud - Todo correcto");
                    case MessageType.Nothing:
                        return "Escloud";
                    case MessageType.NotAvailable:
                        return Get("/tray/not_available", "No disponible");
                }
            }
        }

        public string this[WebUiAction a]
        {
            get
            {
                switch (a)
                {
                    case WebUiAction.waiting:
                        return Get("/web_interface/downloading", "The Web Interface will be downloaded.")
                            + Environment.NewLine +
                            Get("/web_interface/in_a_minute", "This will take a minute.");
                    case WebUiAction.removing:
                        return Get("/web_interface/removing", "Removing the Web Interface...");
                    case WebUiAction.updating:
                        return Get("/web_interface/updating", "Updating the web interface...");
                    case WebUiAction.removed:
                        return Get("/web_interface/removed", "Web interface has been removed.");
                    default:
                        return Get("/web_interface/updated", "Web Interface has been updated.")
                            + Environment.NewLine +
                            Get("/web_interface/setup", "Click here to view and set it up!");
                }
            }
        }

        public string this[ChangeAction ca, bool file]
        {
            get
            {
                string fileorfolder = (file) ? this[MessageType.File] : this[MessageType.Folder];
                switch (ca)
                {
                    case ChangeAction.created:
                        return string.Format(this[MessageType.ItemCreated], fileorfolder);
                    case ChangeAction.deleted:
                        return string.Format(this[MessageType.ItemDeleted], fileorfolder);
                    case ChangeAction.renamed:
                        return this[MessageType.ItemRenamed];
                    case ChangeAction.changed:
                        return string.Format(this[MessageType.ItemChanged], fileorfolder);
                    default:
                        return string.Format(this[MessageType.ItemUpdated], fileorfolder);
                }
            }
        }

        public string this[UiControl c]
        {
            get
            {
                switch(c)
                {
                    // Setup
                    case UiControl.LoginDetails:
                        return Get("/new_account/login_details", "Credenciales de Usuario");
                    case UiControl.Protocol:
                        return Get("/main_form/mode", "Protocolo") + ":";
                    case UiControl.Encryption:
                        return Get("/new_account/encryption", "Encriptación") + ":";
                    case UiControl.Host:
                        return Get("/main_form/host", "Servidor") + ":";
                    case UiControl.Port:
                        return Get("/main_form/port", "Puerto") + ":";
                    case UiControl.Username:
                        return Get("/main_form/username", "Usuario") + ":";
                    case UiControl.Password:
                        return Get("/main_form/password", "Contraseña") + ":";
                    case UiControl.AskForPassword:
                        return Get("/new_account/ask_for_password", "No guardar la contraseña");
                    case UiControl.Authentication:
                        return Get("/setup/authentication", "Autenticación") + ":";
                    case UiControl.LocalFolder:
                        return Get("/paths/local_folder", "Carpeta local");
                    case UiControl.DefaultLocalFolder:
                        return Get("/paths/default_local", "Utilizar la carpeta por defecto");
                    case UiControl.CustomLocalFolder:
                        return Get("/paths/custom_local", "Seleccionar otra carpeta");
                    case UiControl.Browse:
                        return Get("/paths/browse", "Explorar");
                    case UiControl.RemotePath:
                        return Get("/main_form/remote_path", "Ruta remota");
                    case UiControl.FullRemotePath:
                        return Get("/paths/full_path", "Ruta") + ":";
                    case UiControl.SelectiveSync:
                        return Get("/main_form/selective", "Archivos que sincronizar");
                    case UiControl.SyncAllFiles:
                        return Get("/setup/sync_all_files", "Sincronizar todos los archivos");
                    case UiControl.SyncSpecificFiles:
                        return Get("/setup/sync_specific_files", "Seleccionar los archivos que quiero sincronizar");
                    case UiControl.UncheckFiles:
                        return Get("/main_form/selective_info", "Desmarca los archivos que no quieras sincronizar") + ":";
                    case UiControl.Previous:
                        return Get("/setup/previous", "Anterior");
                    case UiControl.Next:
                        return Get("/setup/next", "Siguiente");
                    case UiControl.Finish:
                        return Get("/new_account/done", "Terminar");
                    // Options
                    case UiControl.Options:
                        return Get("/main_form/options", "Opciones");
                    case UiControl.General:
                        return Get("/main_form/general", "General");
                    case UiControl.Links:
                        return Get("/main_form/links", "Enlaces");
                    case UiControl.FullAccountPath:
                        return Get("/main_form/account_full_path", "Ruta completa de cuenta") + ":";
                    case UiControl.WhenRecentFileClicked:
                        return Get("/main_form/when_not_clicked", "Al clicar una notificación") + ":";
                    case UiControl.OpenUrl:
                        return Get("/main_form/open_in_browser", "Abrir enlace en el explorador");
                    case UiControl.CopyUrl:
                        return Get("/main_form/copy", "Copiar enlace al portapapeles");
                    case UiControl.OpenLocal:
                        return Get("/main_form/open_local", "Abrir el archivo");
                    case UiControl.Application:
                        return Get("/main_form/application", "Aplicación");
                    case UiControl.ShowNotifications:
                        return Get("/main_form/show_nots", "Mostrar notificaciones");
                    case UiControl.StartOnStartup:
                        return Get("/main_form/start_on_startup", "Arrancar al inicio");
                    case UiControl.EnableLogging:
                        return Get("/main_form/enable_logging", "Habilitar login");
                    case UiControl.ViewLog:
                        return Get("/main_form/view_log", "Ver Log");
                    case UiControl.AddShellMenu:
                        return Get("/main_form/shell_menus", "Añadir al menú contextual");
                    case UiControl.Account:
                        return Get("/main_form/account", "Cuenta");
                    case UiControl.Profile:
                        return Get("/main_form/profile", "Perfil");
                    case UiControl.Add:
                        return Get("/new_account/add", "Añadir");
                    case UiControl.Remove:
                        return Get("/main_form/remove", "Eliminar");
                    case UiControl.Details:
                        return Get("/main_form/details", "Detalles");
                    case UiControl.WebUi:
                        return Get("/web_interface/web_int", "Interfaz Web");
                    case UiControl.UseWebUi:
                        return Get("/web_interface/use_webint", "Usar la Interfaz Web");
                    case UiControl.ViewInBrowser:
                        return Get("/web_interface/view", "(Ver en explorador)");
                    case UiControl.WayOfSync:
                        return Get("/main_form/way_of_sync", "Modo de sincronización") + ":";
                    case UiControl.LocalToRemoteSync:
                        return Get("/main_form/local_to_remote", "Sólo subir archivos");
                    case UiControl.RemoteToLocalSync:
                        return Get("/main_form/remote_to_local", "Sólo descargar archivos");
                    case UiControl.BothWaysSync:
                        return Get("/main_form/both_ways", "Subir y descargar archivos");
                    case UiControl.TempNamePrefix:
                        return Get("/main_form/temp_file_prefix", "Prefijo temporal") + ":";
                    case UiControl.Filters:
                        return Get("/main_form/file_filters", "Filtros");
                    case UiControl.Configure:
                        return Get("/main_form/configure", "Configurar");
                    case UiControl.Refresh:
                        return Get("/main_form/refresh", "Actualizar");
                    case UiControl.IgnoredExtensions:
                        return Get("/main_form/ignored_extensions", "Extensiones ignoradas");
                    case UiControl.AlsoIgnore:
                        return Get("/main_form/also_ignore", "Ignorar también") + ":";
                    case UiControl.Dotfiles:
                        return Get("/main_form/dotfiles", "dotfiles");
                    case UiControl.TempFiles:
                        return Get("/main_form/temp_files", "Archivos temporales");
                    case UiControl.FilesModifiedBefore:
                        return Get("/main_form/old_files", "Archivos modificados antes") + ":";

                    case UiControl.Bandwidth:
                        return Get("/main_form/bandwidth", "Ancho de banda");
                    case UiControl.SyncFrequency:
                        return Get("/main_form/sync_freq", "Frecuencia de sincronización");
                    case UiControl.SyncWhen:
                        return Get("/main_form/sync_when", "Sincronizar archivos remotos");
                    case UiControl.AutoEvery:
                        return Get("/main_form/auto", "automáticamente cada");
                    case UiControl.Seconds:
                        return Get("/main_form/seconds", "segundos");
                    case UiControl.Manually:
                        return Get("/main_form/manually", "manualmente");
                    case UiControl.SpeedLimits:
                        return Get("/main_form/speed_limits", "Límites de velocidad");
                    case UiControl.DownLimit:
                        return Get("/main_form/limit_download", "Límite de velocidad de descarga");
                    case UiControl.UpLimit:
                        return Get("/main_form/limit_upload", "Límite de velocidad de subida");
                    case UiControl.NoLimits:
                        return Get("/main_form/no_limits", "( 0 es sin límite )");

                    case UiControl.Language:
                        return Get("/main_form/language", "Idioma");

                    case UiControl.About:
                        return Get("/main_form/about", "Acerca de");
                    case UiControl.TheTeam:
                        return Get("/main_form/team", "El equipo") + ":";
                    case UiControl.Website:
                        return Get("/main_form/website", "Sitio web oficial") + ":";
                    case UiControl.Contact:
                        return Get("/main_form/contact", "Contacto") + ":";
                    case UiControl.CodedIn:
                        return Get("/main_form/coded_in", "Codificado en") + ":";
                    case UiControl.Notes:
                        return Get("/main_form/notes", "Notas");
                    case UiControl.Contribute:
                        return Get("/main_form/contribute", "Contribuir");
                    case UiControl.FreeAndAll:
                        return Get("/main_form/ftpbox_is_free", "- FTPbox is gratis y software libre");
                    case UiControl.GetInTouch:
                        return Get("/main_form/contact_me", "- Feel free to contact me for anything.");
                    case UiControl.ReportBug:
                        return Get("/main_form/report_bug", "Informar de un error");
                    case UiControl.RequestFeature:
                        return Get("/main_form/request_feature", "Solicitar una característica");
                    case UiControl.Donate:
                        return Get("/main_form/donate", "Donar") + ":";

                    case UiControl.RecentFiles:
                        return Get("/tray/recent_files", "Archivos recientes");
                    case UiControl.Modified:
                        return Get("/tray/modified", "Modificado");
                    case UiControl.StartSync:
                        return Get("/tray/start_syncing", "Comenzar sincronización");
                    case UiControl.Exit:
                        return Get("/tray/exit", "Salir");

                    // New Version Form
                    case UiControl.UpdateAvailable:
                        return Get("/new_version/update_available", "Actualización Disponible");
                    case UiControl.NewVersionAvailable:
                        return Get("/new_version/new_v_available", "Nueva versión disponible");
                    case UiControl.CurrentVersion:
                        return Get("/new_version/current_version", "Versión Actual");
                    case UiControl.NewVersion:
                        return Get("/new_version/new_ver", "Nueva Versión");
                    case UiControl.AskDownload:
                        return Get("/new_version/wanna_download", "¿Quieres descargar la nueva versión?");
                    case UiControl.DownloadNow:
                        return Get("/new_version/download", "Actualizar");
                    case UiControl.LearnMore:
                        return Get("/new_version/learn_more", "Aprender más");
                    case UiControl.RemindLater:
                        return Get("/new_version/remind_me_next_time", "Ahora no");

                    default:
                        return string.Empty;
                }
            }
        }

        #region parsing from translations file

        public string Get(string xPath, string defaultValue, string lan = null)
        {
            var path = string.Format("translations/{0}{1}", lan ?? Settings.General.Language, xPath);
            XmlNode xmlNode = xmlDocument.SelectSingleNode(path);
            if (xmlNode != null) { return xmlNode.InnerText.Replace("_and", "&"); }
            else { return defaultValue; }
        }

        /// <summary>
        /// Returns a list of all paths to nodes that contain translation strings
        /// </summary>
        public List<string> GetPaths()
        {
            return xmlDocument.SelectNodes("translations/en/*/*").Cast<XmlNode>()
                .Select(x => string.Format("/{0}/{1}", x.ParentNode.Name, x.Name))
                .ToList();
        }
        
        #endregion
    }
}
