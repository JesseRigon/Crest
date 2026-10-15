namespace Grove.Data;

public interface IDbConnectionValidator
{
    Task<DbConnectionValidatorResult> ValidateAsync(DbConnectionValidatorContext context);
}
