﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SlipStream.Client.Agos.Models
{
    public interface IServerConnection : IDisposable
    {
        void BeginConnect(Uri uri, System.Action<Exception> resultCallback);
        void Close();
    }
}