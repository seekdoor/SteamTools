using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace System.Logging
{
    [ProviderAlias("Mac")]
    public class PlatformLoggerProvider : ILoggerProvider
    {
        private PlatformLoggerProvider() { }

        public ILogger CreateLogger(string name)
        {
            return new PlatformLogger(name);
        }

        void IDisposable.Dispose()
        {
        }

        public static ILoggerProvider Instance { get; } = new PlatformLoggerProvider();
    }
}