using Microsoft.Extensions.DependencyInjection;
using Guard.Infrastructure.Interfaces;
using Guard.Infrastructure.Services;

namespace Guard.Infrastructure.IoC
{
  /// <summary>
  /// Модуль для регистрации зависимостей инфраструктурного слоя.
  /// </summary>
  public static class InfrastructureModule
  {
    /// <summary>
    /// Регистрация зависимостей инфраструктурного слоя.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    public static void RegisterInfrastructureServices(this IServiceCollection services)
    {
      // Регистрация репозитория для работы с подразделениями
      services.AddScoped<ISubdivisionRepository, SubdivisionRepository>();

      // Регистрация других сервисов инфраструктурного слоя 
      // (например, логирование, кэширование, отправка почты)
      // ...
    }
  }
}