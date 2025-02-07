// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only 

using System.Collections.Generic;
using Nethermind.AccountAbstraction.Data;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Int256;

namespace Nethermind.AccountAbstraction.Executor
{
    public interface IUserOperationTxBuilder
    {
        Transaction BuildTransaction(
            long gaslimit,
            byte[] callData,
            Address sender,
            BlockHeader parent,
            IReleaseSpec spec,
            UInt256 nonce,
            bool systemTransaction);

        Transaction BuildTransactionFromUserOperations(
            IEnumerable<UserOperation> userOperations,
            BlockHeader parent,
            long gasLimit,
            UInt256 nonce,
            IReleaseSpec spec);

        FailedOp? DecodeEntryPointOutputError(byte[] output);
    }
}
