using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using Metalama.Framework.Advising;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace PointlessWaymarks.LlamaAspects;

public class GenerateStatusCommandsAttribute : TypeAspect
{
    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        foreach (var method in builder.Target.Methods.Where(p =>
                     (p.Attributes.Any(typeof(BlockingCommandAttribute)) ||
                      p.Attributes.Any(typeof(NonBlockingCommandAttribute))) && p.Parameters.Count == 0))
            builder.Advice.IntroduceAutomaticProperty(method.DeclaringType, $"{method.Name}Command",
                TypeFactory.GetType(typeof(RelayCommand)).ToNullable(), IntroductionScope.Default,
                OverrideStrategy.Ignore,
                propertyBuilder => propertyBuilder.Accessibility = Accessibility.Public);

        foreach (var method in builder.Target.Methods.Where(p =>
                     (p.Attributes.Any(typeof(BlockingCommandAttribute)) ||
                      p.Attributes.Any(typeof(NonBlockingCommandAttribute))) && p.Parameters.Count == 1 &&
                     p.Parameters[0].Type.ToType() != typeof(CancellationToken)))
        {
            var firstParameterType = method.Parameters[0].Type;

            builder.Advice.IntroduceAutomaticProperty(method.DeclaringType, $"{method.Name}Command",
                ((INamedType)TypeFactory.GetType(typeof(RelayCommand<>))).WithTypeArguments(firstParameterType)
                .ToNullable(),
                IntroductionScope.Default,
                OverrideStrategy.Ignore,
                propertyBuilder =>
                {
                    propertyBuilder.Accessibility = Accessibility.Public;
                    propertyBuilder.InitializerExpression = null;
                });
        }

        foreach (var method in builder.Target.Methods.Where(p =>
                     p.Attributes.Any(typeof(BlockingCommandAttribute)) && p.Parameters.Count == 2 &&
                     p.Parameters[1].Type.ToType() == typeof(CancellationToken)))
        {
            var firstParameterType = method.Parameters[0].Type;

            builder.Advice.IntroduceAutomaticProperty(method.DeclaringType, $"{method.Name}Command",
                ((INamedType)TypeFactory.GetType(typeof(RelayCommand<>))).WithTypeArguments(firstParameterType)
                .ToNullable(),
                IntroductionScope.Default,
                OverrideStrategy.Ignore,
                propertyBuilder =>
                {
                    propertyBuilder.Accessibility = Accessibility.Public;
                    propertyBuilder.InitializerExpression = null;
                });
        }

        foreach (var method in builder.Target.Methods.Where(p =>
                     (p.Attributes.Any(typeof(BlockingCommandAttribute)) ||
                      p.Attributes.Any(typeof(NonBlockingCommandAttribute))) && p.Parameters.Count == 1 &&
                     p.Parameters[0].Type.ToType() == typeof(CancellationToken)))
            builder.Advice.IntroduceAutomaticProperty(method.DeclaringType, $"{method.Name}Command",
                TypeFactory.GetType(typeof(RelayCommand)).ToNullable(), IntroductionScope.Default,
                OverrideStrategy.Ignore,
                propertyBuilder => propertyBuilder.Accessibility = Accessibility.Public);

        builder.Advice.IntroduceMethod(builder.Target, "BuildCommands");

        //if (builder.Target.Constructors.Count < 1)
        //{
        //    builder.IntroduceConstructor(
        //        nameof(builder.Target),
        //        buildConstructor: c => { });
        //}

        //foreach (var constructor in builder.Target.Constructors)
        //{
        //    builder.With(constructor).Override(nameof(this.OverrideConstructorTemplate));
        //}
    }

    [Template]
    private void OverrideConstructorTemplate()
    {
        meta.Proceed();
        meta.InsertStatement("BuildCommands();");
    }

    [Template]
    public void BuildCommands()
    {
        foreach (var loopMethods in meta.Target.Type.Methods.Where(p =>
                     p.Attributes.Any(typeof(BlockingCommandAttribute)) && p.Parameters.Count == 0))
            meta.InsertStatement(
                $"{loopMethods.Name}Command = StatusContext.RunBlockingTaskCommand({loopMethods.Name});");

        foreach (var loopMethods in meta.Target.Type.Methods.Where(p =>
                     p.Attributes.Any(typeof(NonBlockingCommandAttribute)) && p.Parameters.Count == 0))
            meta.InsertStatement(
                $"{loopMethods.Name}Command = StatusContext.RunNonBlockingTaskCommand({loopMethods.Name});");

        foreach (var loopMethods in meta.Target.Type.Methods.Where(p =>
                     p.Attributes.Any(typeof(BlockingCommandAttribute)) && p.Parameters.Count == 1))
            if (loopMethods.Parameters[0].Type != TypeFactory.GetType(typeof(CancellationToken)))
                meta.InsertStatement(
                    $"{loopMethods.Name}Command = StatusContext.RunBlockingTaskCommand<{loopMethods.Parameters[0].Type}>({loopMethods.Name});");
            else
                meta.InsertStatement(
                    $"{loopMethods.Name}Command = StatusContext.RunBlockingTaskWithCancellationCommand({loopMethods.Name}, \"Cancel {SplitCamelCase(loopMethods.Name)}\");");

        foreach (var loopMethods in meta.Target.Type.Methods.Where(p =>
                     p.Attributes.Any(typeof(BlockingCommandAttribute)) && p.Parameters.Count == 2))
            if (loopMethods.Parameters[1].Type == TypeFactory.GetType(typeof(CancellationToken)))
                meta.InsertStatement(
                    $"{loopMethods.Name}Command = StatusContext.RunBlockingTaskWithCancellationCommand<{loopMethods.Parameters[0].Type}>({loopMethods.Name}, \"Cancel {SplitCamelCase(loopMethods.Name)}\");");

        foreach (var loopMethods in meta.Target.Type.Methods.Where(p =>
                     p.Attributes.Any(typeof(NonBlockingCommandAttribute)) && p.Parameters.Count == 1 &&
                     p.Parameters[0].Type != TypeFactory.GetType(typeof(CancellationToken))))
            if (loopMethods.Parameters[0].Type != TypeFactory.GetType(typeof(CancellationToken)))
                meta.InsertStatement(
                    $"{loopMethods.Name}Command = StatusContext.RunNonBlockingTaskCommand<{loopMethods.Parameters[0].Type}>({loopMethods.Name});");
    }

    private string SplitCamelCase(string str)
    {
        //https://stackoverflow.com/questions/5796383/insert-spaces-between-words-on-a-camel-cased-token
        return Regex.Replace(Regex.Replace(str, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
    }
}