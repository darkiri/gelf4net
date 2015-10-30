﻿using log4net.Appender;
using log4net.Util;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace gelf4net.Appender
{
    public class GelfAmqpAppender : AppenderSkeleton
    {
        public GelfAmqpAppender()
        {
            Encoding = Encoding.UTF8;
        }

        protected ConnectionFactory ConnectionFactory { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public string Exchange { get; set; }
        public string Key { get; set; }
        public string VirtualHost { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Encoding Encoding { get; set; }
        protected IConnection Connection {get;set;}
        protected IModel Channel {get;set;}
        private static volatile object _syncLock = new object();
        
        public override void ActivateOptions()
        {
            base.ActivateOptions();

            InitializeConnectionFactory();
        }

        protected virtual void InitializeConnectionFactory()
        {
            ConnectionFactory = new ConnectionFactory()
            {
                HostName = RemoteAddress,
                Port = RemotePort,
                VirtualHost = VirtualHost,
                UserName = Username,
                Password = Password,
                AutomaticRecoveryEnabled = true
            };
            Connection = ConnectionFactory.CreateConnection();
            Channel = Connection.CreateModel();
        }

        protected override void Append(log4net.Core.LoggingEvent loggingEvent)
        {
            var message = RenderLoggingEvent(loggingEvent).GzipMessage(Encoding);
            byte[] messageBodyBytes = message;
            if (WaitForConnectionToConnectOrReconnect(new TimeSpan(0, 0, 0, 0, 500)))
            {
                lock (_syncLock)
                    Channel.BasicPublish(Exchange, Key, null, messageBodyBytes);
            }
        }

        private bool WaitForConnectionToConnectOrReconnect(TimeSpan timeToWait)
        {
            if (Connection.IsOpen) return true;
            var dt = DateTime.Now;
            while (!Connection.IsOpen && (DateTime.Now - dt) < timeToWait) Thread.Sleep(1);
            return Connection.IsOpen;
        }

        protected override void OnClose()
        {
            Channel.Close();
            Channel.Dispose();
            Connection.Close();
            Connection.Dispose();
        }
    }
}
