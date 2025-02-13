// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Core.Crypto;
using Nethermind.DataMarketplace.Consumers.DataAssets.Domain;
using Nethermind.DataMarketplace.Consumers.Notifiers;
using Nethermind.DataMarketplace.Consumers.Providers.Repositories;
using Nethermind.DataMarketplace.Core.Domain;
using Nethermind.Logging;

namespace Nethermind.DataMarketplace.Consumers.DataAssets.Services
{
    public class DataAssetService : IDataAssetService
    {
        private readonly ConcurrentDictionary<Keccak, DataAsset> _discoveredDataAssets =
            new ConcurrentDictionary<Keccak, DataAsset>();

        private readonly IProviderRepository _providerRepository;
        private readonly IConsumerNotifier _consumerNotifier;
        private readonly ILogger _logger;

        public DataAssetService(IProviderRepository providerRepository, IConsumerNotifier consumerNotifier,
            ILogManager logManager)
        {
            _providerRepository = providerRepository;
            _consumerNotifier = consumerNotifier;
            _logger = logManager.GetClassLogger();
        }

        public bool IsAvailable(DataAsset dataAsset)
            => dataAsset.State == DataAssetState.Published || dataAsset.State == DataAssetState.UnderMaintenance;

        public DataAsset? GetDiscovered(Keccak dataAssetId)
            => _discoveredDataAssets.TryGetValue(dataAssetId, out DataAsset? dataAsset) ? dataAsset : null;

        public IReadOnlyList<DataAsset> GetAllDiscovered()
            => _discoveredDataAssets.Values.Where(h => h.State == DataAssetState.Published ||
                                                       h.State == DataAssetState.UnderMaintenance).ToArray();

        public Task<IReadOnlyList<DataAssetInfo>> GetAllKnownAsync()
            => _providerRepository.GetDataAssetsAsync();

        public void AddDiscovered(DataAsset dataAsset, INdmPeer peer)
        {
            _discoveredDataAssets.TryAdd(dataAsset.Id, dataAsset);
        }

        public void AddDiscovered(DataAsset[] dataAssets, INdmPeer peer)
        {
            for (var i = 0; i < dataAssets.Length; i++)
            {
                var dataAsset = dataAssets[i];
                _discoveredDataAssets.TryAdd(dataAsset.Id, dataAsset);
            }
        }

        public void ChangeState(Keccak dataAssetId, DataAssetState state)
        {
            if (!_discoveredDataAssets.TryGetValue(dataAssetId, out var dataAsset))
            {
                return;
            }

            dataAsset.SetState(state);
            _consumerNotifier.SendDataAssetStateChangedAsync(dataAssetId, dataAsset.Name, state);
            if (_logger.IsInfo) _logger.Info($"Changed the discovered data asset: '{dataAssetId}' state to: '{state}'.");
        }

        public void RemoveDiscovered(Keccak dataAssetId)
        {
            if (!_discoveredDataAssets.TryRemove(dataAssetId, out var dataAsset))
            {
                return;
            }

            _consumerNotifier.SendDataAssetRemovedAsync(dataAssetId, dataAsset.Name);
        }
    }
}
