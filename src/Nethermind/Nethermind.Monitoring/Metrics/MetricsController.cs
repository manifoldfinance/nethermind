// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using Nethermind.Monitoring.Config;
using Prometheus;

namespace Nethermind.Monitoring.Metrics
{
    public class MetricsController : IMetricsController
    {
        private readonly int _intervalSeconds;
        private Timer _timer;
        private readonly Dictionary<Type, (PropertyInfo, string)[]> _propertiesCache = new();
        private readonly Dictionary<Type, (FieldInfo, string)[]> _fieldsCache = new();
        private readonly Dictionary<Type, (string DictName, IDictionary<string, long> Dict)> _dynamicPropCache = new();
        private readonly HashSet<Type> _metricTypes = new();

        public readonly Dictionary<string, Gauge> _gauges = new();

        public void RegisterMetrics(Type type)
        {
            EnsurePropertiesCached(type);
            foreach ((PropertyInfo propertyInfo, string gaugeName) in _propertiesCache[type])
            {
                _gauges[gaugeName] = CreateMemberInfoMectricsGauge(propertyInfo);
            }

            foreach ((FieldInfo fieldInfo, string gaugeName) in _fieldsCache[type])
            {
                _gauges[gaugeName] = CreateMemberInfoMectricsGauge(fieldInfo);
            }

            _metricTypes.Add(type);
        }

        private Gauge CreateMemberInfoMectricsGauge(MemberInfo propertyInfo)
        {
            GaugeConfiguration configuration = new();

            configuration.StaticLabels = propertyInfo
                .GetCustomAttributes<MetricsStaticDescriptionTagAttribute>()
                .ToDictionary(
                    attribute => attribute.Label,
                    attribute => GetStaticMemberInfo(attribute.Informer, attribute.Label));

            string description = propertyInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;
            string name = BuildGaugeName(propertyInfo);
            return CreateGauge(name, description, configuration);
        }

        private static string GetStaticMemberInfo(Type givenInformer, string givenName)
        {
            Type type = givenInformer;
            PropertyInfo[] tagsData = type.GetProperties(BindingFlags.Static | BindingFlags.Public);
            PropertyInfo info = tagsData.FirstOrDefault(info => info.Name == givenName);
            if (info is null)
            {
                throw new NotSupportedException("Developer error: a requested static description field was not implemented!");
            }

            object value = info.GetValue(null);
            if (value is null)
            {
                throw new NotSupportedException("Developer error: a requested static description field was not initialised!");
            }

            return value.ToString();
        }

        private void EnsurePropertiesCached(Type type)
        {
            if (!_propertiesCache.ContainsKey(type))
            {
                _propertiesCache[type] = type.GetProperties().Where(p => !p.PropertyType.IsAssignableTo(typeof(System.Collections.IEnumerable))).Select(
                    p => (p, GetGaugeNameKey(type.Name, p.Name))).ToArray();
            }

            if (!_fieldsCache.ContainsKey(type))
            {
                _fieldsCache[type] = type.GetFields().Where(f => !f.FieldType.IsAssignableTo(typeof(System.Collections.IEnumerable))).Select(
                    f => (f, GetGaugeNameKey(type.Name, f.Name))).ToArray();
            }

            if (!_dynamicPropCache.ContainsKey(type))
            {
                var p = type.GetProperties().FirstOrDefault(p => p.PropertyType.IsAssignableTo(typeof(IDictionary<string, long>)));
                if (p != null)
                {
                    _dynamicPropCache[type] = (p.Name, (IDictionary<string, long>)p.GetValue(null));
                }
            }
        }

        private static string BuildGaugeName(MemberInfo propertyInfo) =>
            propertyInfo.GetCustomAttribute<DataMemberAttribute>()?.Name ?? BuildGaugeName(propertyInfo.Name);

        private static string BuildGaugeName(string propertyName) =>
            $"nethermind_{Regex.Replace(propertyName, @"(\p{Ll})(\p{Lu})", "$1_$2").ToLowerInvariant()}";

        private static Gauge CreateGauge(string name, string help = "", GaugeConfiguration configuration = null)
            => Prometheus.Metrics.CreateGauge(name, help, configuration);

        public MetricsController(IMetricsConfig metricsConfig)
        {
            _intervalSeconds = metricsConfig.IntervalSeconds == 0 ? 5 : metricsConfig.IntervalSeconds;
        }

        public void StartUpdating()
        {
            _timer = new Timer(UpdateMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(_intervalSeconds));
        }

        public void StopUpdating()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }

        public void UpdateMetrics(object state)
        {
            foreach (Type metricType in _metricTypes)
            {
                UpdateMetrics(metricType);
            }
        }

        private void UpdateMetrics(Type type)
        {
            EnsurePropertiesCached(type);

            foreach ((PropertyInfo propertyInfo, string gaugeName) in _propertiesCache[type])
            {
                double value = Convert.ToDouble(propertyInfo.GetValue(null));
                ReplaceValueIfChanged(value, gaugeName);
            }

            foreach ((FieldInfo fieldInfo, string gaugeName) in _fieldsCache[type])
            {
                double value = Convert.ToDouble(fieldInfo.GetValue(null));
                ReplaceValueIfChanged(value, gaugeName);
            }

            if (_dynamicPropCache.TryGetValue(type, out var dict))
            {
                foreach (var kvp in dict.Dict)
                {
                    double value = Convert.ToDouble(kvp.Value);
                    var gaugeName = GetGaugeNameKey(dict.DictName, kvp.Key);

                    if (ReplaceValueIfChanged(value, gaugeName) is null)
                    {
                        var gauge = CreateGauge(BuildGaugeName(kvp.Key));
                        _gauges[gaugeName] = gauge;
                        gauge.Set(value);
                    }
                }
            }

            Gauge ReplaceValueIfChanged(double value, string gaugeName)
            {
                if (_gauges.TryGetValue(gaugeName, out var gauge))
                {
                    if (Math.Abs(gauge.Value - value) > double.Epsilon)
                    {
                        gauge.Set(value);
                    }
                }

                return gauge;
            }
        }

        private string GetGaugeNameKey(params string[] par)
        {
            return string.Join('.', par);
        }
    }
}
