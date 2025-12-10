using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace MaterialClient.Common;

public abstract class MaterialClientTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", false);
        builder.AddJsonFile("appsettings.secrets.json", true);
        services.ReplaceConfiguration(builder.Build());
    }

    protected virtual Task WithUnitOfWorkAsync(Func<Task> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task WithUnitOfWorkAsync(AbpUnitOfWorkOptions options, Func<Task> action)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                try
                {
                    await action();
                    await uow.CompleteAsync();
                }
                catch (Exception ex)
                {
                    var exceptionMessage = $"Exception occurred in WithUnitOfWorkAsync. " +
                                         $"UoW Options: IsTransactional={options.IsTransactional}, " +
                                         $"Timeout={options.Timeout}ms. " +
                                         $"Original exception: {ex.GetType().Name} - {ex.Message}";
                    
                    throw new InvalidOperationException(exceptionMessage, ex);
                }
            }
        }
    }

    protected virtual Task<TResult> WithUnitOfWorkAsync<TResult>(Func<Task<TResult>> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(AbpUnitOfWorkOptions options,
        Func<Task<TResult>> func)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                try
                {
                    var result = await func();
                    await uow.CompleteAsync();
                    return result;
                }
                catch (Exception ex)
                {
                    var exceptionMessage = $"Exception occurred in WithUnitOfWorkAsync<{typeof(TResult).Name}>. " +
                                         $"UoW Options: IsTransactional={options.IsTransactional}, " +
                                         $"Timeout={options.Timeout}ms. " +
                                         $"Original exception: {ex.GetType().Name} - {ex.Message}";
                    
                    throw new InvalidOperationException(exceptionMessage, ex);
                }
            }
        }
    }
}

