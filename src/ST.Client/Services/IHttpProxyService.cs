using System;
using System.Application.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Properties;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;

namespace System.Application.Services
{
    public interface IHttpProxyService
    {
        public bool IsCertificate { get; }

        public void TrustCer();

        public IReadOnlyCollection<AccelerateProjectDTO>? ProxyDomains { get; set; }

        public IReadOnlyCollection<ScriptDTO>? Scripts { get; set; }

        public bool IsEnableScript { get; set; }

        public bool IsOnlyWorkSteamBrowser { get; set; }

        public string CertificateName { get; set; }

        public CertificateEngine CertificateEngine { get; set; }

        public int ProxyPort { get; set; }

        public IPAddress ProxyIp { get; set; }

        public bool IsWindowsProxy { get; set; }

        public bool IsProxyGOG { get; set; }

        public bool OnlyEnableProxyScript { get; set; }

        public bool Socks5ProxyEnable { get; set; }

        public int Socks5ProxyPortId { get; set; }
        public int HostProxyPortId { get; set; }
        

        public bool TwoLevelAgentEnable { get; set; }

        public ExternalProxyType TwoLevelAgentProxyType { get; set; }


        public const ExternalProxyType DefaultTwoLevelAgentProxyType = ExternalProxyType.Socks5;

        public string? TwoLevelAgentIp { get; set; }

        public int TwoLevelAgentPortId { get; set; }

        public string? TwoLevelAgentUserName { get; set; }

        public string? TwoLevelAgentPassword { get; set; }

        public bool ProxyRunning { get; }

        public bool SetupCertificate();

        public bool DeleteCertificate();

        bool PortInUse(int port);

        public bool StartProxy();

        public void StopProxy();

        public bool WirtePemCertificateToGoGSteamPlugins();

        public bool IsCertificateInstalled(X509Certificate2? certificate2);

        public void Dispose();
    }
}