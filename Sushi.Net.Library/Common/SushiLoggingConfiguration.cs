using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library.Common
{
    public class SushiLoggingConfiguration :  ILoggingConfiguration, ILoggingConfigurationProviderCollection
    {
        private readonly List<ILoggingConfigurationProvider> _providers;
        public LogLevel LastLevel { get; private set; }
        /// <summary>
        /// Initializes new instance of <see cref="LoggingConfiguration"/>.
        /// </summary>
        public SushiLoggingConfiguration()
        {
            _providers = new List<ILoggingConfigurationProvider>();
        }

        /// <inheritdoc />
        public void SetLevel(LogLevel level, string? category = null, string? provider = null)
        {
            LastLevel=level;
            foreach (var p in _providers)
            {
                p.SetLevel(level, category, provider);
            }
        }

        /// <inheritdoc />
        public void ResetLevel(string? category = null, string? provider = null)
        {
            foreach (var p in _providers)
            {
                p.ResetLevel(category, provider);
            }
        }

        /// <inheritdoc />
        int ILoggingConfigurationProviderCollection.Count => _providers.Count;

        /// <inheritdoc />
        void ILoggingConfigurationProviderCollection.Add(ILoggingConfigurationProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _providers.Add(provider);
        }

        /// <inheritdoc />
        void ILoggingConfigurationProviderCollection.Remove(ILoggingConfigurationProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            _providers.Remove(provider);
        }
    }
}

