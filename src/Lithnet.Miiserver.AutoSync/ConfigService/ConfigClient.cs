﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Lithnet.Miiserver.AutoSync
{
    public class ConfigClient : ClientBase<IConfigService>, IConfigService
    {
        public ConfigClient()
            : base(ConfigServiceConfiguration.NetNamedPipeBinding, ConfigServiceConfiguration.NetNamedPipeEndpointAddress)
        {
        }

        public ConfigFile GetConfig()
        {
            ProtectedString.EncryptOnWrite = false;
            ConfigFile x = this.Channel.GetConfig();
            x.ValidateManagementAgents();
            return x;
        }

        public void PutConfig(ConfigFile config)
        {
            ProtectedString.EncryptOnWrite = false;
            this.Channel.PutConfig(config);
        }

        public void PutConfigAndReloadChanged(ConfigFile config)
        {
            this.Channel.PutConfigAndReloadChanged(config);
        }

        public bool IsPendingRestart()
        {
            return this.Channel.IsPendingRestart();
        }

        public void Stop(string managementAgentName, bool cancelRun)
        {
            this.Channel.Stop(managementAgentName, cancelRun);
        }

        public void CancelRun(string managementAgentName)
        {
            this.Channel.CancelRun(managementAgentName);
        }

        public void Start(string managementAgentName)
        {
            this.Channel.Start(managementAgentName);
        }

        public void StopAll(bool cancelRuns)
        {
            this.Channel.StopAll(cancelRuns);
        }

        public void StartAll()
        {
            this.Channel.StartAll();
        }

        public ControlState GetEngineState()
        {
            return this.Channel.GetEngineState();
        }
        public IList<string> GetManagementAgentNames()
        {
            return this.Channel.GetManagementAgentNames();
        }

        public IList<string> GetManagementAgentRunProfileNames(string managementAgentName)
        {
            return this.Channel.GetManagementAgentRunProfileNames(managementAgentName);
        }

        public void AddToExecutionQueue(string managementAgentName, string runProfileName)
        {
            this.Channel.AddToExecutionQueue(managementAgentName, runProfileName);
        }

        public IList<string> GetManagementAgentsPendingRestart()
        {
            return this.Channel.GetManagementAgentsPendingRestart();
        }

        public void RestartChangedExecutors()
        {
            this.Channel.RestartChangedExecutors();
        }

        public void SetAutoStartState(bool autoStart)
        {
            this.Channel.SetAutoStartState(autoStart);
        }

        public bool GetAutoStartState()
        {
            return this.Channel.GetAutoStartState();
        }
    }
}