using System.Data.Common;
using CMS.API.Infrastructure;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public sealed class CourseRepository(IDbConnectionFactory connectionFactory, IRowAuditWriter auditWriter)
    : ICourseRepository
{
    private const string TableName = "Course";
    private const int SqlForeignKeyViolation = 547;

    private const string SelectColumns = """
        SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl, c.DisplayOrder,
               c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid, c.PublishStatus_pkid AS PublishStatusPkid,
               c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
               c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline, c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
               p.Name AS PartnerName, g.Description AS CourseGroupDescription, s.Description AS PublishStatusDescription
        FROM Course c
        INNER JOIN Partner p ON p.pkid = c.Partner_pkid
        LEFT JOIN CourseGroup g ON g.pkid = c.CourseGroup_pkid
        INNER JOIN PublishStatus s ON s.pkid = c.PublishStatus_pkid
        """;

    private const string DefaultOrder = " ORDER BY c.DisplayOrder ASC, c.pkid DESC";

    public async Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Course>(
            new CommandDefinition(SelectColumns + DefaultOrder, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken)
    {
        const string sql = SelectColumns + """

            WHERE (@Keyword IS NULL
                   OR c.Title LIKE '%' + @Keyword + '%'
                   OR c.OfficialTitle LIKE '%' + @Keyword + '%'
                   OR c.CourseId LIKE '%' + @Keyword + '%'
                   OR c.ProdCourseId LIKE '%' + @Keyword + '%'
                   OR c.FriendlyUrl LIKE '%' + @Keyword + '%')
              AND (@PartnerPkid IS NULL OR c.Partner_pkid = @PartnerPkid)
              AND (@CourseGroupPkid IS NULL OR c.CourseGroup_pkid = @CourseGroupPkid)
              AND (@PublishStatusPkid IS NULL OR c.PublishStatus_pkid = @PublishStatusPkid)
              AND (@ScheduleOnFrom IS NULL OR c.ScheduleOn >= @ScheduleOnFrom)
              AND (@ScheduleOnTo IS NULL OR c.ScheduleOn <= @ScheduleOnTo)
              AND (@ScheduleOffFrom IS NULL OR c.ScheduleOff >= @ScheduleOffFrom)
              AND (@ScheduleOffTo IS NULL OR c.ScheduleOff <= @ScheduleOffTo)
              AND (@CanRepeat IS NULL OR c.CanRepeat = @CanRepeat)
            """ + DefaultOrder;

        var parameters = new
        {
            Keyword = string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
            query.PartnerPkid,
            query.CourseGroupPkid,
            query.PublishStatusPkid,
            query.ScheduleOnFrom,
            query.ScheduleOnTo,
            query.ScheduleOffFrom,
            query.ScheduleOffTo,
            query.CanRepeat
        };

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Course>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(connection, pkid, cancellationToken);
    }

    public async Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
                                Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour,
                                ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
                                TowardCertOrExam, Note, OtherInfo, CanRepeat)
            VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
                    @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
                    @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
                    @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var parameters = ToParameters(request);
        var pkid = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

        await SyncCertificationsAsync(connection, transaction, pkid, request.CertificationPkids, cancellationToken);
        await SyncJobCategoriesAsync(connection, transaction, pkid, request.JobCategoryPkids, cancellationToken);

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Insert,
            parameters.CourseId, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Course
            SET Title = @Title,
                OfficialTitle = @OfficialTitle,
                CourseId = @CourseId,
                ProdCourseId = @ProdCourseId,
                FriendlyUrl = @FriendlyUrl,
                DisplayOrder = @DisplayOrder,
                Partner_pkid = @PartnerPkid,
                CourseGroup_pkid = @CourseGroupPkid,
                PublishStatus_pkid = @PublishStatusPkid,
                ScheduleOn = @ScheduleOn,
                ScheduleOff = @ScheduleOff,
                Hour = @Hour,
                ListPrice = @ListPrice,
                LearningCredit = @LearningCredit,
                Material = @Material,
                Objective = @Objective,
                Target = @Target,
                Prerequisites = @Prerequisites,
                Outline = @Outline,
                TowardCertOrExam = @TowardCertOrExam,
                Note = @Note,
                OtherInfo = @OtherInfo,
                CanRepeat = @CanRepeat
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

        var certificationPkids = Normalize(request.CertificationPkids);
        var jobCategoryPkids = Normalize(request.JobCategoryPkids);
        await SyncCertificationsAsync(connection, transaction, request.Pkid, certificationPkids, cancellationToken);
        await SyncJobCategoriesAsync(connection, transaction, request.Pkid, jobCategoryPkids, cancellationToken);

        // Scalar diff (shared property names only — the parameter object has no label / list members),
        // then the two junctions compared as sets.
        var changed = AuditHelper.ChangedColumns(existing, parameters).ToList();
        if (!Normalize(existing.CertificationPkids).SequenceEqual(certificationPkids))
        {
            changed.Add(nameof(Course.CertificationPkids));
        }
        if (!Normalize(existing.JobCategoryPkids).SequenceEqual(jobCategoryPkids))
        {
            changed.Add(nameof(Course.JobCategoryPkids));
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
            // CourseInCertification / CourseJobCategories cascade; CourseFAQ, CourseRelatedLink and HotCourse do not.
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Course WHERE pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new EntityInUseException($"課程 {pkid}「{existing.CourseId}」仍被課程問答、相關連結或熱門課程使用，無法刪除。");
        }

        await auditWriter.WriteAsync(connection, TableName, pkid.ToString(), RowAuditWriter.Delete,
            existing.CourseId, cancellationToken, transaction);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<LookupItem>> GetLookupAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition(
            "SELECT pkid AS Pkid, CourseId + N' ' + Title AS Label FROM Course ORDER BY CourseId ASC, pkid ASC",
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    private static async Task<Course?> GetByIdAsync(
        DbConnection connection,
        int pkid,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var course = await connection.QuerySingleOrDefaultAsync<Course>(new CommandDefinition(
            SelectColumns + " WHERE c.pkid = @Pkid", new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        if (course is null)
        {
            return null;
        }

        var certifications = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        course.CertificationPkids = certifications.AsList();

        var jobCategories = await connection.QueryAsync<short>(new CommandDefinition(
            "SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        course.JobCategoryPkids = jobCategories.AsList();

        return course;
    }

    private static async Task SyncCertificationsAsync(
        DbConnection connection, DbTransaction transaction, int pkid, IEnumerable<int> certificationPkids, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(certificationPkids).Select(id => new { Pkid = pkid, CertificationPkid = id }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @CertificationPkid)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static async Task SyncJobCategoriesAsync(
        DbConnection connection, DbTransaction transaction, int pkid, IEnumerable<short> jobCategoryPkids, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        var rows = Normalize(jobCategoryPkids).Select(id => new { Pkid = pkid, JobCategoryPkid = id }).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid)",
            rows, transaction, cancellationToken: cancellationToken));
    }

    private static List<T> Normalize<T>(IEnumerable<T>? ids) where T : struct, IComparable<T> =>
        (ids ?? []).Distinct().OrderBy(id => id).ToList();

    /// <summary>
    /// Scalar-only parameter object: trims strings, blanks optional text to NULL. Having no label / list
    /// members keeps <see cref="AuditHelper.ChangedColumns"/> to the real columns.
    /// </summary>
    private static CourseScalars ToParameters(CourseRequest request) => new()
    {
        Pkid = request.Pkid,
        Title = request.Title.Trim(),
        OfficialTitle = NullIfBlank(request.OfficialTitle),
        CourseId = request.CourseId.Trim(),
        ProdCourseId = request.ProdCourseId.Trim(),
        FriendlyUrl = request.FriendlyUrl.Trim(),
        DisplayOrder = request.DisplayOrder,
        PartnerPkid = request.PartnerPkid,
        CourseGroupPkid = request.CourseGroupPkid,
        PublishStatusPkid = request.PublishStatusPkid,
        ScheduleOn = request.ScheduleOn,
        ScheduleOff = request.ScheduleOff,
        Hour = request.Hour,
        ListPrice = request.ListPrice,
        LearningCredit = request.LearningCredit,
        Material = NullIfBlank(request.Material),
        Objective = NullIfBlank(request.Objective),
        Target = NullIfBlank(request.Target),
        Prerequisites = NullIfBlank(request.Prerequisites),
        Outline = NullIfBlank(request.Outline),
        TowardCertOrExam = NullIfBlank(request.TowardCertOrExam),
        Note = NullIfBlank(request.Note),
        OtherInfo = NullIfBlank(request.OtherInfo),
        CanRepeat = request.CanRepeat
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CourseScalars
    {
        public int Pkid { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? OfficialTitle { get; init; }
        public string CourseId { get; init; } = string.Empty;
        public string ProdCourseId { get; init; } = string.Empty;
        public string FriendlyUrl { get; init; } = string.Empty;
        public int DisplayOrder { get; init; }
        public short PartnerPkid { get; init; }
        public short? CourseGroupPkid { get; init; }
        public byte PublishStatusPkid { get; init; }
        public DateOnly ScheduleOn { get; init; }
        public DateOnly ScheduleOff { get; init; }
        public short Hour { get; init; }
        public decimal ListPrice { get; init; }
        public decimal LearningCredit { get; init; }
        public string? Material { get; init; }
        public string? Objective { get; init; }
        public string? Target { get; init; }
        public string? Prerequisites { get; init; }
        public string? Outline { get; init; }
        public string? TowardCertOrExam { get; init; }
        public string? Note { get; init; }
        public string? OtherInfo { get; init; }
        public bool CanRepeat { get; init; }
    }
}
