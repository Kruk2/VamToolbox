using System;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Autofac;
using VamRepacker.Hashing;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Operations.Abstract;
using VamRepacker.Operations.NotDestructive;
using VamRepacker.Sqlite;

namespace VamRepackerUi
{
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
            Application.Run(container.Resolve<MainWindow>());
        }

        private static void CatchUnhandledDomain(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show((e.ExceptionObject as Exception).ToString(), "Unhandled UI Exception");
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
            builder.RegisterType<JsonScannerHelper>().As<IJsonFileParser>().SingleInstance();
            builder.RegisterType<MainWindow>().As<IProgressTracker>().AsSelf().SingleInstance();

            builder.RegisterType<Logger>().As<ILogger>().InstancePerLifetimeScope();
            builder.RegisterType<Database>().As<IDatabase>().OnActivating(t => t.Instance.Open(System.AppContext.BaseDirectory)).InstancePerLifetimeScope();
            builder.RegisterType<MD5Helper>().As<IHashingAlgo>().SingleInstance();

            builder.RegisterType<PresetGrouper>().As<IPresetGrouper>();
            builder.RegisterType<MorphGrouper>().As<IMorphGrouper>();
            builder.RegisterType<PreviewGrouper>().As<IPreviewGrouper>();
            builder.RegisterType<ScriptGrouper>().As<IScriptGrouper>();
            builder.RegisterType<JsonUpdater>().As<IJsonUpdater>();


            var assembly = typeof(ScanVarPackagesOperation).Assembly;
            builder
                .RegisterAssemblyTypes(assembly)
                .AssignableTo<IOperation>()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            return builder.Build();
        }
    }
}
