﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Lithnet.Common.Presentation;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;
using PropertyChanged;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{
    internal class MainWindowViewModel : ViewModelBase
    {
        private bool confirmedCloseOnDirtyViewModel;

        internal ExecutionMonitorsViewModel ExecutionMonitor { get; set; }

        public ConfigFileViewModel ConfigFile { get; set; }

        public string DisplayName => "Lithnet AutoSync" + (App.IsConnectedToLocalhost() ? string.Empty : $" - {App.Hostname}") + (this.IsDirty ? "*" : string.Empty);

        public Cursor Cursor { get; set; }

        [DependsOn(nameof(ConfigFile))]
        public override IEnumerable<ViewModelBase> ChildNodes
        {
            get
            {
                yield return this.ExecutionMonitor;
                yield return this.ConfigFile?.ManagementAgents;
                yield return this.ConfigFile?.Settings;
            }
        }

        public Visibility StatusBarVisibility { get; set; }

        public string RevertOrUndoLabel
        {
            get
            {
                if (this.IsDirty)
                {
                    return "Und_o pending changes";
                }
                else
                {
                    return "Rel_oad config from server";
                }
            }
        }

        public MainWindowViewModel()
        {
            this.AddDependentPropertyNotification("IsDirty", nameof(this.DisplayName));
            this.AddDependentPropertyNotification("IsDirty", nameof(this.RevertOrUndoLabel));

            this.Commands.AddItem("Save", "_Commit changes", new KeyGesture(Key.S, ModifierKeys.Control), x => this.Save(), x => this.CanSave());
            this.Commands.AddItem("Revert", "Re_load config", new KeyGesture(Key.R, ModifierKeys.Control), x => this.Revert());
            this.Commands.AddItem("Export", "_Backup configuration...", new KeyGesture(Key.B, ModifierKeys.Control | ModifierKeys.Shift), x => this.Export(), x => this.CanExport());
            this.Commands.AddItem("Close", "C_lose...", new KeyGesture(Key.X, ModifierKeys.Control), x => this.Close());
            this.Commands.AddItem("Help", "_Online help", new KeyGesture(Key.F1, ModifierKeys.None), x => this.Help());
            this.Commands.AddItem("About", "_About...", x => this.About());
            this.Commands.AddItem("Import", "R_estore configuration...", new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift), x => this.Import(), x => this.CanImport());


            this.StatusBarVisibility = Visibility.Collapsed;

            this.Cursor = Cursors.Arrow;
            Application.Current.MainWindow.Closing += this.MainWindow_Closing;

            ViewModelBase.ViewModelIsDirtySet += this.ViewModelBase_ViewModelIsDirtySet;
            this.SetupExecutionMonitors();
        }

        private void SetupExecutionMonitors()
        {
            IDictionary<Guid, string> maNames = new Dictionary<Guid, string>();

            try
            {
                ConfigClient c = App.GetDefaultConfigClient();
                Debug.WriteLine("Getting management agent names");
                c.InvokeThenClose(x => maNames = x.GetManagementAgentNameIDs());
                Debug.WriteLine("Got management agent names");
                this.ExecutionMonitor = new ExecutionMonitorsViewModel(maNames.Cast<object>().ToList());
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not contact the AutoSync service. The execution monitor could not be set up.", "AutoSync service unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Could not setup execution monitors");
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not setup the execution monitor\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewModelBase_ViewModelIsDirtySet(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"A view model of type '{sender.GetType().Name}' changed its state to dirty based on a change of property '{e.PropertyName}'");

            if (!this.IsDirty)
            {
                this.ConfigFile.Version++;
                this.IsDirty = true;
                Trace.WriteLine($"Setting view model state to dirty based on a change of property '{e.PropertyName}' on an object of type '{sender.GetType().Name}'");
            }
        }

        private void Help()
        {
            try
            {
                Process.Start(App.HelpBaseUrl);
            }
            catch
            {
            }
        }

        private void About()
        {
            Windows.About w = new Windows.About();
            w.ShowDialog();
        }

        private void Revert()
        {
            if (this.IsDirty)
            {
                if (MessageBox.Show("This will undo any pending changes and restore the configuration to the last saved state. Are you sure you want to continue?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.ResetConfigViewModel();
        }

        internal void ResetConfigViewModel()
        {
            try
            {
                this.Cursor = Cursors.Wait;

                ConfigFile file = null;


                ConfigClient c = App.GetDefaultConfigClient();
                c.InvokeThenClose(x => file = x.GetConfig());

                if (file == null)
                {
                    this.ConfigFile = null;
                }
                else
                {
                    this.ConfigFile = new ConfigFileViewModel(file);
                    this.ConfigFile.ManagementAgents.IsExpanded = true;
                }

                this.IsDirty = false;
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not contact the AutoSync service", "AutoSync service unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                this.ConfigFile = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception refreshing view");
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not reload the configuration from the server\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.ConfigFile = null;
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void Import()
        {
            this.UpdateFocusedBindings();

            if (!this.ContinueOnUnsavedChanges())
            {
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".xml";
            dialog.Filter = "Configuration Files (.xml)|*.xml|All Files|*.*";
            dialog.CheckFileExists = true;

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            if (!System.IO.File.Exists(dialog.FileName))
            {
                return;
            }

            ConfigFile f;
            try
            {
                f = Serializer.Read<ConfigFile>(dialog.FileName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not open the file\n\n{ex.Message}", "File Open", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                this.Cursor = Cursors.Wait;
                ConfigClient c = App.GetDefaultConfigClient();
                f = c.ValidateConfig(f);
                c.InvokeThenClose(x => x.PutConfig(f));
                this.AskToRestartService();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not import the file\n\n{ex.Message}", "File import operation", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }

            this.IsDirty = false;

            this.ResetConfigViewModel();
        }

        private void Save()
        {
            this.UpdateFocusedBindings();

            try
            {
                if (this.ConfigFile.HasErrors)
                {
                    MessageBox.Show("There are one or more errors in the config file that must be fixed before the file can be saved");
                    return;
                }

                this.Cursor = Cursors.Wait;

                this.CommitConfig();

                this.AskToRestartService();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the configuration\n\n{ex.Message}", "Save operation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void CommitConfig()
        {
            try
            {
                ConfigClient c = App.GetDefaultConfigClient();
                c.InvokeThenClose(t => t.PutConfig(this.ConfigFile.Model));
                this.Commit();
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not contact the AutoSync service. The save operation could not be completed", "AutoSync service unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                this.ConfigFile = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not commit the configuration\n\n{ex.Message}", "Save operation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Commit()
        {
            this.IsDirty = false;
            this.ConfigFile.Commit();
        }

        private bool CanSave()
        {
            return this.ConfigFile != null && this.IsDirty;
        }

        private bool CanImport()
        {
            return this.ConfigFile != null;
        }

        private bool CanExport()
        {
            return this.ConfigFile != null;
        }

        private void AskToRestartService()
        {
            try
            {
                ConfigClient c = App.GetDefaultConfigClient();
                List<Guid> pendingRestartGuids = c.GetManagementAgentsPendingRestart()?.ToList();

                if (pendingRestartGuids != null && pendingRestartGuids.Count > 0)
                {
                    IDictionary<Guid, string> mapping = c.GetManagementAgentNameIDs();

                    IEnumerable<string> pendingRestartItems = mapping.Where(t => pendingRestartGuids.Contains(t.Key)).Select(t => t.Value);

                    string pendingRestartItemList = string.Join("\r\n", pendingRestartItems);

                    if (MessageBox.Show($"The configuration for the following management agents have changed\r\n\r\n{pendingRestartItemList}\r\n\r\nDo you want to restart them now?", "Restart management agents", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            c.InvokeThenClose(t => t.RestartChangedControllers());
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine("Error restarting controllers");
                            Trace.WriteLine(ex.ToString());
                            MessageBox.Show($"Could not restart the management agents\n\n{ex.Message}", "Restart management agents", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not contact the AutoSync service", "AutoSync service unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
                this.ConfigFile = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Error determining controllers to restart");
                Trace.WriteLine(ex.ToString());
                MessageBox.Show($"Could not determine if any management agents required a restart\n\n{ex.Message}", "Restart management agents", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export()
        {
            this.UpdateFocusedBindings();

            if (this.HasErrors)
            {
                if (MessageBox.Show("There are one or more errors present in the configuration. Are you sure you want to save?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (MessageBox.Show("Any passwords in the configuration will be exported in plain-text. Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.DefaultExt = ".xml";
                dialog.Filter = "Configuration file backups (*.xml)|*.xml|All Files|*.*";

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    this.Cursor = Cursors.Wait;
                    ProtectedString.EncryptOnWrite = false;
                    AutoSync.ConfigFile.Save(this.ConfigFile.Model, dialog.FileName);
                    this.IsDirty = false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the file\n\n{ex.Message}", "Save File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.UpdateFocusedBindings();

            if (!this.ContinueOnUnsavedChanges())
            {
                e.Cancel = true;
            }
        }

        private void Close()
        {
            this.UpdateFocusedBindings();

            if (!this.ContinueOnUnsavedChanges())
            {
                return;
            }

            this.confirmedCloseOnDirtyViewModel = true;
            Application.Current.Shutdown();
        }

        private bool ContinueOnUnsavedChanges()
        {
            if (this.IsDirty && !this.confirmedCloseOnDirtyViewModel)
            {
                MessageBoxResult result = MessageBox.Show("Do you want to commit the unsaved changes?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        this.CommitConfig();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("An error occurred trying to commit the changes");
                        Trace.WriteLine(ex);
                        MessageBox.Show($"An error occurred committing the changes\r\n\r\n{ex.Message}", "Error committing changes", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    this.AskToRestartService();
                }
            }

            return true;
        }

        private void UpdateFocusedBindings()
        {
            object focusedItem = Keyboard.FocusedElement;

            if (focusedItem == null)
            {
                return;
            }

            BindingExpression expression = (focusedItem as TextBox)?.GetBindingExpression(TextBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as ComboBox)?.GetBindingExpression(ComboBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as PasswordBox)?.GetBindingExpression(PasswordBoxBindingHelper.PasswordProperty);
            expression?.UpdateSource();

            expression = (focusedItem as TimeSpanControl)?.GetBindingExpression(TimeSpanControl.ValueProperty);
            expression?.UpdateSource();

            expression = (focusedItem as DateTimePicker)?.GetBindingExpression(DateTimePicker.SelectedDateProperty);
            expression?.UpdateSource();
        }
    }
}
