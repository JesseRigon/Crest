namespace Crest.Workflows.Models;

internal record SignalHandlerRegistration(Type SignalType, Func<object, SignalContext, ValueTask> Handler);