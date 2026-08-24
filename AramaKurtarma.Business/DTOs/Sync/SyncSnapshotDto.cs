namespace AramaKurtarma.Business.DTOs.Sync;

/// <summary>
/// Ilk kurulumda mobilin cektigi TAM paket. Modules ve Contents duz (flat)
/// listeler - ic ice degil, cunku mobil tarafta SQLite gibi duz tablolara
/// yazilmalari daha kolay. Contents kendi ModuleId'siyle hangi module ait
/// oldugunu, Blocks ise her Content'in icinde tasinir.
/// </summary>
public sealed record SyncSnapshotDto(
    int Version,
    SyncBookDto Book,
    IReadOnlyList<SyncModuleDto> Modules,
    IReadOnlyList<SyncContentDto> Contents);
