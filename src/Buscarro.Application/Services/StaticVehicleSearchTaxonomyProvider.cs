using Buscarro.Application.DTOs;
using Buscarro.Application.Interfaces;

namespace Buscarro.Application.Services;

public sealed class StaticVehicleSearchTaxonomyProvider : IVehicleSearchTaxonomyProvider
{
    private static readonly VehicleSearchTaxonomy Taxonomy = new(
        Colors: Array.AsReadOnly(
        [
            "amarelo", "azul", "bege", "branco", "bronze", "cinza", "dourado", "indefinida",
            "nada", "marrom", "prata", "preto", "rosa", "roxo", "verde", "vermelho", "vinho"
        ]),
        Gears: Array.AsReadOnly(
        [
            "automático", "automático / sequencial", "automático cvt", "manual", "semi-automático"
        ]),
        Fuels: Array.AsReadOnly(
        [
            "gasolina", "álcool", "Diesel", "gás natural", "gasolina e álcool",
            "gasolina e gás natural", "álcool e gás natural", "gasolina, álcool e GNV",
            "elétrico e gasolina", "elétrico"
        ]));

    public Task<VehicleSearchTaxonomy> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Taxonomy);
    }
}
