namespace Crest.Data;

public interface IDbConnectionValidator
{
    Task<DbConnectionValidatorResult> ValidateAsync(DbConnectionValidatorContext context);
}
