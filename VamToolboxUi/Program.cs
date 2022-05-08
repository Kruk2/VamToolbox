using System.IO.Abstractions;
using Autofac;
using VamToolbox.FilesGrouper;
using VamToolbox.Hashing;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.NotDestructive;
using VamToolbox.Sqlite;

namespace VamToolboxUi;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        Application.ThreadException += CatchUnhandled;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += CatchUnhandledDomain;

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var container = Configure();
        EnsureDbCreated(container);
        Application.Run(container.Resolve<MainWindow>());
    }

    private static void EnsureDbCreated(IContainer container)
    {
        using var scope = container.BeginLifetimeScope();
        scope.Resolve<IDatabase>().EnsureCreated();
    }

    private static void CatchUnhandledDomain(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show((e.ExceptionObject as Exception)!.ToString(), "Unhandled UI Exception");
    }

    private static void CatchUnhandled(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Unhandled Thread Exception");
    }

    private static IContainer Configure()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<FileSystem>().As<IFileSystem>().SingleInstance();
        builder.RegisterType<SoftLinker>().As<ISoftLinker>().SingleInstance();
        builder.RegisterType<JsonFileParser>().As<IJsonFileParser>().SingleInstance();
        builder.RegisterType<MainWindow>().As<IProgressTracker>().AsSelf().SingleInstance();

        builder.RegisterType<Logger>().As<ILogger>().InstancePerLifetimeScope();
        builder.Register(_ => new Database(System.AppContext.BaseDirectory)).As<IDatabase>().InstancePerLifetimeScope();
        builder.RegisterType<MD5Helper>().As<IHashingAlgo>().SingleInstance();

        builder.RegisterType<PresetGrouper>().As<IPresetGrouper>();
        builder.RegisterType<MorphGrouper>().As<IMorphGrouper>();
        builder.RegisterType<PreviewGrouper>().As<IPreviewGrouper>();
        builder.RegisterType<ScriptGrouper>().As<IScriptGrouper>();
        builder.RegisterType<JsonUpdater>().As<IJsonUpdater>();
        builder.RegisterType<ReferenceCacheReader>().As<IReferenceCacheReader>();
        builder.RegisterType<UuidReferencesResolver>().As<IUuidReferenceResolver>();
        builder.RegisterType<ReferencesResolver>().As<IReferencesResolver>();

        var assembly = typeof(ScanVarPackagesOperation).Assembly;
        builder
            .RegisterAssemblyTypes(assembly)
            .AssignableTo<IOperation>()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        return builder.Build();
    }
}