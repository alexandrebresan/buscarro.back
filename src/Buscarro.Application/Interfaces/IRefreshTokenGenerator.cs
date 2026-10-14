namespace Buscarro.Application.Interfaces;
public interface IRefreshTokenGenerator
{
    string Generate(); string Hash(string value);
}
