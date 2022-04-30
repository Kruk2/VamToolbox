using AutoFixture;
using AutoFixture.Kernel;
using VamToolbox.Models;

namespace VamToolbox.Tests;

public sealed class CustomFixture : Fixture
{
    public CustomFixture()
    {
        Customize<OpenedPotentialJson>(c => c.Without(c => c.Stream));
        Customizations.Add(new TypeRelay( typeof(FileReferenceBase), typeof(FreeFile)));
        Customizations.Add(new TypeRelay( typeof(FileReferenceBase), typeof(VarPackageFile)));
        Customizations.Add(new VarNameBuilder());
    }


    internal class VarNameBuilder : ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request is Type type && type == typeof(VarPackageName))
            {
                var author = context.Create<string>();
                var varName = context.Create<string>();
                var version = context.Create<int>();
                if(VarPackageName.TryGet($"{author}.{varName}.{version}.var", out var parsedVar))
                    return parsedVar;
            }

            return new NoSpecimen();
        }
    }
}