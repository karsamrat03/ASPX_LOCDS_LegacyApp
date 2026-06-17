using System;
using System.Configuration;
using FluentValidation;
using LOCDS.BLL;
using LOCDS.BLL.DTOs;
using LOCDS.BLL.Validators;
using LOCDS.DAL.Abstractions;
using LOCDS.DAL.Connection;
using LOCDS.DAL.Repositories;
using Microsoft.Extensions.Logging;
using Unity;
using Unity.Lifetime;

namespace LOCDS.Web.App_Start
{
    public static class UnityConfig
    {
        private static readonly Lazy<IUnityContainer> Container = new Lazy<IUnityContainer>(BuildContainer);

        public static IUnityContainer CurrentContainer => Container.Value;

        public static void RegisterComponents()
        {
            _ = CurrentContainer;
        }

        private static IUnityContainer BuildContainer()
        {
            var container = new UnityContainer();
            var setting = ConfigurationManager.ConnectionStrings["LOCDSConnection"];
            var connectionString = setting != null ? setting.ConnectionString : null;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'LOCDSConnection' is missing in Web.config.");
            }

            container.RegisterFactory<IDbConnectionFactory>(
                _ => new SqlConnectionFactory(connectionString),
                new PerRequestLifetimeManager());

            container.RegisterType<IValidator<SubmitApplicationDto>, SubmitApplicationDtoValidator>(new TransientLifetimeManager());
            container.RegisterType<IValidator<ApplicationDashboardFilterDto>, ApplicationDashboardFilterDtoValidator>(new TransientLifetimeManager());
            container.RegisterType<IValidator<ManualDecisionDto>, ManualDecisionDtoValidator>(new TransientLifetimeManager());
            container.RegisterType<IValidator<AuditLogRequestDto>, AuditLogRequestDtoValidator>(new TransientLifetimeManager());

            container.RegisterType<ILoanApplicationRepository, LoanApplicationRepository>(new PerRequestLifetimeManager());
            container.RegisterType<ICreditBureauRepository, CreditBureauRepository>(new PerRequestLifetimeManager());
            container.RegisterType<IUnderwritingRepository, UnderwritingRepository>(new PerRequestLifetimeManager());
            container.RegisterType<IUnitOfWork, SqlUnitOfWork>(new PerRequestLifetimeManager());

            container.RegisterFactory<ILoggerFactory>(
                _ => LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                }),
                new ContainerControlledLifetimeManager());

            container.RegisterType<ILoanApplicationService, LoanApplicationService>(new TransientLifetimeManager());
            container.RegisterType<ICreditScoringService, CreditScoringService>(new TransientLifetimeManager());
            container.RegisterType<IUnderwritingService, UnderwritingService>(new TransientLifetimeManager());
            container.RegisterType<ILoanOfferService, LoanOfferService>(new TransientLifetimeManager());

            container.RegisterType<IAuditService, AuditService>(new ContainerControlledLifetimeManager());

            return container;
        }
    }
}
