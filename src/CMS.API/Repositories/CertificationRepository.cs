using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class CertificationRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : ICertificationRepository
{
    private const string TableName = "Certification";
    private const int SqlForeignKeyViolation = 547;
    private const string UntitledLabel = "(無名稱)";

    // Title is nchar(100) → RTRIM everywhere it is read.
    private const string SelectColumns = """
        SELECT c.pkid, c.Partner_pkid AS PartnerPkid, RTRIM(c.Title) AS Title,
               p.Name AS PartnerName
        FROM Certification c
        INNER JOIN Partner p ON p.pkid = c.Partner_pkid
        """;

    private const string DefaultOrder = " ORDER BY c.pkid DESC";

    public async Task<IReadOnlyList<Certification>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Certification>(
            new CommandDefinition(SelectColumns + DefaultOrder, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Certification>> QueryAsync(CertificationQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL OR RTRIM(c.Title) LIKE '%' + @Keyword + '%')
              AND (@PartnerPkid IS NULL OR c.Partner_pkid = @PartnerPkid)
              AND (@CoursePkid IS NULL OR EXISTS (
                    SELECT 1 FROM CourseInCertification j
                    WHERE j.Certification_pkid = c.pkid AND j.Course_pkid = @CoursePkid))
              AND (@JobCategoryPkid IS NULL OR EXISTS (
                    SELECT 1 FROM CertificationJobCategories j
                    WHERE j.Certification_pkid = c.pkid AND j.JobCategory_pkid = @JobCategoryPkid))
            """ + DefaultOrder;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.PartnerPkid,
            query.CoursePkid,
            query.JobCategoryPkid
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Certification>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Certification?> GetByIdAsync(int pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<int> CreateAsync(CertificationRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Certification (Partner_pkid, Title)
            VALUES (@PartnerPkid, @Title);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var parameters = ToParameters(request);
        var pkid = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

        await SyncCoursesAsync(connection, transaction, pkid, request.CoursePkids, cancellationToken);
        await SyncJobCategoriesAsync(connection, transaction, pkid, request.JobCategoryPkids, cancellationToken);

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Insert,
            parameters.Title ?? UntitledLabel, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(CertificationRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Certification
            SET Partner_pkid = @PartnerPkid,
                Title = @Title
            WHERE pkid = @Pkid;
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, request.Pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        var parameters = ToParameters(request);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            return false;
        }

        var coursePkids = Normalize(request.CoursePkids);
        var jobCategoryPkids = Normalize(request.JobCategoryPkids);
        await SyncCoursesAsync(connection, transaction, request.Pkid, coursePkids, cancellationToken);
        await SyncJobCategoriesAsync(connection, transaction, request.Pkid, jobCategoryPkids, cancellationToken);

        // Scalar diff (shared property names only), then the two junctions compared as sets.
        var changed = AuditHelper.ChangedColumns(existing, parameters).ToList();
        if (!Normalize(existing.CoursePkids).SequenceEqual(coursePkids))
        {
            changed.Add(nameof(Certification.CoursePkids));
        }
        if (!Normalize(existing.JobCategoryPkids).SequenceEqual(jobCategoryPkids))
        {
            changed.Add(nameof(Certification.JobCategoryPkids));
        }

        await auditWriter.WriteAsync(connection, TableName, request.Pkid.ToString(), RowAuditWriter.Update,
            changed.Count == 0 ? "(no changes)" : string.Join(", ", changed), cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await GetByIdAsync(connection, pkid, cancellationToken, transaction);
        if (existing is null)
        {
            return false;
        }

        try
        {
            // The junctions are payload-free links owned by this feature's multiselects, so they go with the row.
            // FK_CourseInCertification_Certification does not cascade; CertificationJobCategories does, but is
            // deleted explicitly for symmetry.
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE FROM CourseInCertification WHERE Certification_pkid = @Pkid;
                DELETE FROM CertificationJobCategories WHERE Certification_pkid = @Pkid;
                DELETE FROM Certification WHERE pkid = @Pkid;
                """, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"認證 {pkid}「{existing.Title ?? UntitledLabel}」仍被其他資料使用，無法刪除。");
        }

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Delete,
            existing.Title ?? UntitledLabel, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        // Title is nchar(100) and nullable → RTRIM + ISNULL.
        const string sql = """
            SELECT c.pkid AS Pkid, p.Name + N' ' + ISNULL(RTRIM(c.Title), N'') AS Label
            FROM Certification c
            INNER JOIN Partner p ON p.pkid = c.Partner_pkid
            ORDER BY p.DisplayOrder ASC, p.Name ASC, RTRIM(c.Title) ASC, c.pkid ASC
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static async Task<Certification?> GetByIdAsync(
        DbConnection connection,
        int pkid,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var certification = await connection.QuerySingleOrDefaultAsync<Certification>(new CommandDefinition(
            SelectColumns + " WHERE c.pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        if (certification is null)
        {
            return null;
        }

        var courses = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Course_pkid FROM CourseInCertification WHERE Certification_pkid = @Pkid ORDER BY Course_pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        certification.CoursePkids = courses.AsList();

        var jobCategories = await connection.QueryAsync<short>(new CommandDefinition(
            "SELECT JobCategory_pkid FROM CertificationJobCategories WHERE Certification_pkid = @Pkid ORDER BY JobCategory_pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        certification.JobCategoryPkids = jobCategories.AsList();

        return certification;
    }

    private static async Task SyncCoursesAsync(
        DbConnection connection, DbTransaction transaction, int pkid, IEnumerable<int> coursePkids, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Certification_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(coursePkids).Select(id => new { Pkid = pkid, CoursePkid = id }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@CoursePkid, @Pkid)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static async Task SyncJobCategoriesAsync(
        DbConnection connection, DbTransaction transaction, int pkid, IEnumerable<short> jobCategoryPkids, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CertificationJobCategories WHERE Certification_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(jobCategoryPkids).Select(id => new { Pkid = pkid, JobCategoryPkid = id }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CertificationJobCategories (Certification_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static List<T> Normalize<T>(IEnumerable<T>? ids) where T : struct, IComparable<T> =>
        (ids ?? []).Distinct().OrderBy(id => id).ToList();

    /// <summary>
    /// Scalar-only parameter object: trims Title and blanks it to NULL. Having no label / list members keeps
    /// <see cref="AuditHelper.ChangedColumns"/> to the real columns.
    /// </summary>
    private static CertificationScalars ToParameters(CertificationRequest request) => new()
    {
        Pkid = request.Pkid,
        PartnerPkid = request.PartnerPkid,
        Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim()
    };

    private sealed class CertificationScalars
    {
        public int Pkid { get; init; }
        public short PartnerPkid { get; init; }
        public string? Title { get; init; }
    }
}
