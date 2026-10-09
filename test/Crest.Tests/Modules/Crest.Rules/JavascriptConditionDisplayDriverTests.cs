using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Notify;
using Crest.Rules.Drivers;
using Crest.Rules.Models;
using Crest.Rules.Services;
using Crest.Rules.ViewModels;
using Crest.Scripting;
using Crest.Scripting.JavaScript;

namespace Crest.Tests.Modules.Crest.Rules;

public class JavascriptConditionDisplayDriverTests
{
    [Theory]
    [InlineData("if (true")] // Unbalanced parenthesis.
    [InlineData("return true; )")] // Stray token.
    public async Task UpdateAsync_WhenScriptCannotBeParsed_AddsValidationError(string script)
    {
        var (driver, notifier) = CreateDriver();
        var condition = new JavascriptCondition
        {
            ConditionId = "1",
            Script = "return false;",
        };

        var context = CreateUpdateContext(script);

        await driver.UpdateAsync(condition, context);

        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.Contains(nameof(JavascriptConditionViewModel.Script)));
        notifier.Verify(n => n.AddAsync(NotifyType.Error, It.IsAny<LocalizedHtmlString>()), Times.Once());

        // The invalid script must not be persisted on the condition.
        Assert.Equal("return false;", condition.Script);
    }

    [Fact]
    public async Task UpdateAsync_WhenScriptThrows_AddsValidationError()
    {
        var (driver, notifier) = CreateDriver();
        var condition = new JavascriptCondition
        {
            ConditionId = "1",
            Script = "return false;",
        };

        var context = CreateUpdateContext("throw new Error('nope');");

        await driver.UpdateAsync(condition, context);

        Assert.False(context.Updater.ModelState.IsValid);
        notifier.Verify(n => n.AddAsync(NotifyType.Error, It.IsAny<LocalizedHtmlString>()), Times.Once());
        Assert.Equal("return false;", condition.Script);
    }

    [Fact]
    public async Task UpdateAsync_WhenScriptIsValid_UpdatesTheCondition()
    {
        var (driver, notifier) = CreateDriver();
        var condition = new JavascriptCondition
        {
            ConditionId = "1",
            Script = "return false;",
        };

        var context = CreateUpdateContext("return true;");

        await driver.UpdateAsync(condition, context);

        Assert.True(context.Updater.ModelState.IsValid);
        notifier.Verify(n => n.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()), Times.Never());
        Assert.Equal("return true;", condition.Script);
    }

    private static UpdateEditorContext CreateUpdateContext(string script)
        => new(null, null, false, string.Empty, null, null, new StubUpdater(script));

    private static (JavascriptConditionDisplayDriver Driver, Mock<INotifier> Notifier) CreateDriver()
    {
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddScripting()
            .AddJavaScriptEngine();

        var serviceProvider = services.BuildServiceProvider();

        var evaluator = new JavascriptConditionEvaluator(
            serviceProvider.GetRequiredService<IScriptingManager>(),
            serviceProvider);

        var notifier = new Mock<INotifier>();

        var driver = new JavascriptConditionDisplayDriver(
            new StubHtmlLocalizer<JavascriptConditionDisplayDriver>(),
            new StubStringLocalizer<JavascriptConditionDisplayDriver>(),
            evaluator,
            notifier.Object);

        return (driver, notifier);
    }

    private sealed class StubUpdater : IUpdateModel
    {
        private readonly string _script;

        public StubUpdater(string script)
        {
            _script = script;
        }

        public ModelStateDictionary ModelState { get; } = new ModelStateDictionary();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class
            => Bind(model);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class
            => Bind(model);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions) where TModel : class
            => Bind(model);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;

        private Task<bool> Bind<TModel>(TModel model) where TModel : class
        {
            if (model is JavascriptConditionViewModel viewModel)
            {
                viewModel.Script = _script;
            }

            return Task.FromResult(true);
        }
    }

    private sealed class StubStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class StubHtmlLocalizer<T> : IHtmlLocalizer<T>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, false, arguments);

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
