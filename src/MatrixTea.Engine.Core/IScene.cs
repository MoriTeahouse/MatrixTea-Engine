// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
namespace MatrixTea.Engine.Core;

/// <summary>
/// Minimal scene contract used by MatrixTea runtimes.
/// </summary>
public interface IScene
{
    string Name { get; }

    void Enter(EngineContext context);

    void Exit(EngineContext context);

    void Update(EngineContext context, TimeSpan delta);

    void Render(EngineContext context, IRenderSurface surface);
}
