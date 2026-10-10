using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Crest.ContentManagement.Metadata;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Descriptor.Models;
using Crest.Environment.Shell.Scope;
using Crest.Lucene.Model;
using YesSql;

namespace Crest.Search.Lucene;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ShellDescriptor _shellDescriptor;

    public Migrations(
        IContentDefinitionManager contentDefinitionManager,
        ShellDescriptor shellDescriptor)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _shellDescriptor = shellDescriptor;
    }

    // New installations don't need to be upgraded, but because there is no initial migration record,
    // 'UpgradeAsync()' is called in a new 'CreateAsync()' but only if the feature was already installed.
    public async Task<int> CreateAsync()
    {
        if (_shellDescriptor.WasFeatureAlreadyInstalled("Crest.Search.Lucene"))
        {
            await UpgradeAsync();
        }

        // Shortcut other migration steps on new content definition schemas.
        return 1;
    }

    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var dbConnectionAccessor = scope.ServiceProvider.GetService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetService<ILogger<Migrations>>();
            var documentTableName = session.Store.Configuration.TableNameConvention.GetDocumentTable();
            var table = $"{session.Store.Configuration.TablePrefix}{documentTableName}";

            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync(session.Store.Configuration.IsolationLevel);
            var dialect = session.Store.Configuration.SqlDialect;

            try
            {
                logger.LogDebug("Updating Lucene serialized type names to the new Crest.Lucene namespace");

                var quotedTableName = dialect.QuoteForTableName(table, session.Store.Configuration.Schema);
                var quotedContentColumnName = dialect.QuoteForColumnName("Content");
                var quotedTypeColumnName = dialect.QuoteForColumnName("Type");

                var updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Search.Lucene.LuceneQuery, Crest.Search.Lucene\"', '\"$type\":\"Crest.Lucene.LuceneQuery, Crest.Lucene\"') WHERE {quotedTypeColumnName} = 'Crest.Queries.Services.QueriesDocument, Crest.Queries'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexDeploymentStep, Crest.Search.Lucene\"', '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexDeploymentStep, Crest.Lucene\"') WHERE {quotedTypeColumnName} = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneSettingsDeploymentStep, Crest.Search.Lucene\"', '\"$type\":\"Crest.Lucene.Deployment.LuceneSettingsDeploymentStep, Crest.Lucene\"') WHERE {quotedTypeColumnName} = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexResetDeploymentStep, Crest.Search.Lucene\"', '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexResetDeploymentStep, Crest.Lucene\"') WHERE {quotedTypeColumnName} = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexRebuildDeploymentStep, Crest.Search.Lucene\"', '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexRebuildDeploymentStep, Crest.Lucene\"') WHERE {quotedTypeColumnName} = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedTypeColumnName} = 'Crest.Lucene.Model.LuceneIndexSettingsDocument, Crest.Lucene' WHERE {quotedTypeColumnName} = 'Crest.Search.Lucene.Model.LuceneIndexSettingsDocument, Crest.Search.Lucene'";
                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                logger.LogError(e, "An error occurred while updating Lucene serialized type names");

                throw;
            }
        });

        return 2;
    }

    // Upgrade an existing installation.
    private async Task UpgradeAsync()
    {
        var contentTypeDefinitions = await _contentDefinitionManager.LoadTypeDefinitionsAsync();

        foreach (var contentTypeDefinition in contentTypeDefinitions)
        {
            foreach (var partDefinition in contentTypeDefinition.Parts)
            {
                await _contentDefinitionManager.AlterPartDefinitionAsync(partDefinition.Name, partBuilder =>
                {
                    if (partDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingPartSettings) &&
                        !partDefinition.Settings.ContainsKey(nameof(LuceneContentIndexSettings)))
                    {
                        var included = existingPartSettings["Included"];
                        var analyzed = existingPartSettings["Analyzed"];

                        if (included is not null)
                        {
                            if (analyzed is not null)
                            {
                                if ((bool)included && !(bool)analyzed)
                                {
                                    existingPartSettings["Keyword"] = true;
                                }
                            }
                            else
                            {
                                if ((bool)included)
                                {
                                    existingPartSettings["Keyword"] = true;
                                }
                            }
                        }

                        var jExistingPartSettings = existingPartSettings.AsObject();

                        // We remove unnecessary properties from old releases.
                        jExistingPartSettings.Remove("Analyzed");
                        jExistingPartSettings.Remove("Tokenized");
                        jExistingPartSettings.Remove("Template");

                        partDefinition.Settings[nameof(LuceneContentIndexSettings)] = jExistingPartSettings.Clone();
                    }

                    partDefinition.Settings.Remove("ContentIndexSettings");
                });
            }
        }

        var partDefinitions = await _contentDefinitionManager.LoadPartDefinitionsAsync();

        foreach (var partDefinition in partDefinitions)
        {
            await _contentDefinitionManager.AlterPartDefinitionAsync(partDefinition.Name, partBuilder =>
            {
                if (partDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingPartSettings) &&
                    !partDefinition.Settings.ContainsKey(nameof(LuceneContentIndexSettings)))
                {
                    var included = existingPartSettings["Included"];
                    var analyzed = existingPartSettings["Analyzed"];

                    if (included != null)
                    {
                        if (analyzed != null)
                        {
                            if ((bool)included && !(bool)analyzed)
                            {
                                existingPartSettings["Keyword"] = true;
                            }
                        }
                        else
                        {
                            if ((bool)included)
                            {
                                existingPartSettings["Keyword"] = true;
                            }
                        }
                    }

                    var jExistingPartSettings = existingPartSettings.AsObject();

                    // We remove unnecessary properties from old releases.
                    jExistingPartSettings.Remove("Analyzed");
                    jExistingPartSettings.Remove("Tokenized");
                    jExistingPartSettings.Remove("Template");
                    partDefinition.Settings[nameof(LuceneContentIndexSettings)] = jExistingPartSettings.Clone();
                }

                partDefinition.Settings.Remove("ContentIndexSettings");

                foreach (var fieldDefinition in partDefinition.Fields)
                {
                    if (fieldDefinition.Settings.TryGetPropertyValue("ContentIndexSettings", out var existingFieldSettings) &&
                        !fieldDefinition.Settings.TryGetPropertyValue(nameof(LuceneContentIndexSettings), out _))
                    {
                        var included = existingFieldSettings["Included"];
                        var analyzed = existingFieldSettings["Analyzed"];

                        if (included != null)
                        {
                            if (analyzed != null)
                            {
                                if ((bool)included && !(bool)analyzed)
                                {
                                    existingFieldSettings["Keyword"] = true;
                                }
                            }
                            else
                            {
                                if ((bool)included)
                                {
                                    existingFieldSettings["Keyword"] = true;
                                }
                            }
                        }

                        var jExistingFieldSettings = existingFieldSettings.AsObject();

                        // We remove unnecessary properties from old releases.
                        jExistingFieldSettings.Remove("Analyzed");
                        jExistingFieldSettings.Remove("Tokenized");
                        jExistingFieldSettings.Remove("Template");

                        fieldDefinition.Settings.Add(nameof(LuceneContentIndexSettings), jExistingFieldSettings.Clone());
                    }

                    fieldDefinition.Settings.Remove("ContentIndexSettings");
                }
            });
        }

        // Defer this until after the subsequent migrations have succeeded as the schema has changed.
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();
            var dbConnectionAccessor = scope.ServiceProvider.GetService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetService<ILogger<Migrations>>();
            var tablePrefix = session.Store.Configuration.TablePrefix;
            var documentTableName = session.Store.Configuration.TableNameConvention.GetDocumentTable();
            var table = $"{session.Store.Configuration.TablePrefix}{documentTableName}";

            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync(session.Store.Configuration.IsolationLevel);
            var dialect = session.Store.Configuration.SqlDialect;

            try
            {
                logger.LogDebug("Updating Lucene indices settings and queries");

                var quotedTableName = dialect.QuoteForTableName(table, session.Store.Configuration.Schema);
                var quotedContentColumnName = dialect.QuoteForColumnName("Content");
                var quotedTypeColumnName = dialect.QuoteForColumnName("Type");

                var updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Lucene.LuceneQuery, Crest.Lucene\"', '\"$type\":\"Crest.Search.Lucene.LuceneQuery, Crest.Search.Lucene\"') WHERE {quotedTypeColumnName}  = 'Crest.Queries.Services.QueriesDocument, Crest.Queries'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexDeploymentStep, Crest.Lucene\"', '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexDeploymentStep, Crest.Search.Lucene\"') WHERE {quotedTypeColumnName}  = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Lucene.Deployment.LuceneSettingsDeploymentStep, Crest.Lucene\"', '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneSettingsDeploymentStep, Crest.Search.Lucene\"') WHERE {quotedTypeColumnName}  = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexResetDeploymentStep, Crest.Lucene\"', '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexResetDeploymentStep, Crest.Search.Lucene\"') WHERE {quotedTypeColumnName}  = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedContentColumnName} = REPLACE({quotedContentColumnName}, '\"$type\":\"Crest.Lucene.Deployment.LuceneIndexRebuildDeploymentStep, Crest.Lucene\"', '\"$type\":\"Crest.Search.Lucene.Deployment.LuceneIndexRebuildDeploymentStep, Crest.Search.Lucene\"') WHERE {quotedTypeColumnName}  = 'Crest.Deployment.DeploymentPlan, Crest.Deployment.Abstractions'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                updateCmd = $"UPDATE {quotedTableName} SET {quotedTypeColumnName} = 'Crest.Search.Lucene.Model.LuceneIndexSettingsDocument, Crest.Search.Lucene' WHERE {quotedTypeColumnName}  = 'Crest.Lucene.Model.LuceneIndexSettingsDocument, Crest.Lucene'";

                await transaction.Connection.ExecuteAsync(updateCmd, null, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync();
                logger.LogError(e, "An error occurred while updating Lucene indices settings and queries");

                throw;
            }
        });
    }
}
