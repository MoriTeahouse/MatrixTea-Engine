// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core.Rhythm;

public sealed record BeatmapBreakPeriod
{
    public double Start { get; init; }

    public double End { get; init; }
}
