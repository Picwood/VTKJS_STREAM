// PrePoMax - Copyright (C) 2016-2026 Matej Borovinšek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.
//
// Source code: https://gitlab.com/MatejB/PrePoMax
//
// Author: Matej Borovinšek
// Contributors:

namespace CaeJob.Native;

/// <summary>Pollable counts of decoded characters drained, including output beyond the capture limit.
/// Use one instance per invocation. Counts are independent snapshots, not percent completion.</summary>
public sealed class NativeProcessProgress
{
    private long _standardOutputCharacters;
    private long _standardErrorCharacters;
    public long StandardOutputCharacters => Interlocked.Read(ref _standardOutputCharacters);
    public long StandardErrorCharacters => Interlocked.Read(ref _standardErrorCharacters);
    internal void Add(int count, bool isError)
    {
        if (isError) Interlocked.Add(ref _standardErrorCharacters, count);
        else Interlocked.Add(ref _standardOutputCharacters, count);
    }
}
