using Buscarro.Application.DTOs;

namespace Buscarro.Tests;

internal static class TestVehicleSearchTaxonomy
{
    public static VehicleSearchTaxonomy Default { get; } =
        new(
            Colors:
            [
                "amarelo",
                "azul",
                "bege",
                "branco",
                "bronze",
                "cinza",
                "dourado",
                "indefinida",
                "nada",
                "marrom",
                "prata",
                "preto",
                "rosa",
                "roxo",
                "verde",
                "vermelho",
                "vinho"
            ],
            Gears:
            [
                "automático",
                "automático / sequencial",
                "automático cvt",
                "manual",
                "semi-automático"
            ],
            Fuels:
            [
                "gasolina",
                "álcool",
                "Diesel",
                "gás natural",
                "gasolina e álcool",
                "gasolina e gás natural",
                "álcool e gás natural",
                "gasolina, álcool e GNV",
                "elétrico e gasolina",
                "elétrico"
            ]);
}