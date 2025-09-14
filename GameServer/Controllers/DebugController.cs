using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using ClosedXML.Excel;
using GameServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Secure this controller
    public class DebugController : ControllerBase
    {
        private readonly GameDbContext _context;
        private readonly ILogger<DebugController> _logger;

        public DebugController(GameDbContext context, ILogger<DebugController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("tables")]
        public async Task<ActionResult<IEnumerable<string>>> GetTableNames()
        {
            try
            {
                var tableNames = await Task.Run(() =>
                {
                    var names = _context
                        .Model.GetEntityTypes()
                        .Select(t =>
                            t.GetSchema() != null
                                ? $"{t.GetSchema()}.{t.GetTableName()}"
                                : t.GetTableName()
                        )
                        .Where(name => name != null)
                        .Distinct()
                        .ToList();

                    // Filter out EF Core migration history table if present
                    names = names
                        .Where(name => name != null && !name.Contains("__EFMigrationsHistory"))
                        .ToList();

                    return names;
                });

                return Ok(tableNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table names.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("table-data/{tableName}")] // New endpoint
        public async Task<IActionResult> GetTableData(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            try
            {
                var entityType = _context
                    .Model.GetEntityTypes()
                    .FirstOrDefault(e =>
                        (
                            e.GetSchema() != null
                                ? $"{e.GetSchema()}.{e.GetTableName()}"
                                : e.GetTableName()
                        ) == tableName
                    );

                if (entityType == null)
                {
                    return NotFound($"Table '{tableName}' not found or not mapped in DbContext.");
                }

                string schema = entityType.GetSchema() ?? "dbo";
                string? actualTableName = entityType.GetTableName();
                if (actualTableName == null)
                {
                    return StatusCode(500, "Table name metadata missing.");
                }

                string sqlQuery = $"SELECT * FROM [{schema}].[{actualTableName}]";

                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = sqlQuery;
                    _context.Database.OpenConnection();

                    var data = new List<Dictionary<string, object>>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columnNames.Add(reader.GetName(i));
                        }

                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                row[columnNames[i]] = val!;
                            }
                            data.Add(row);
                        }
                    }
                    _context.Database.CloseConnection();
                    return Ok(data); // Return data as JSON
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting data for table '{tableName}'.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPost("export-excel")]
        public async Task<IActionResult> ExportToExcel([FromBody] string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            try
            {
                // Validate table name against known entity types to prevent SQL injection
                // This part needs to be updated to handle schema-qualified names
                var entityType = _context
                    .Model.GetEntityTypes()
                    .FirstOrDefault(e =>
                        (
                            e.GetSchema() != null
                                ? $"{e.GetSchema()}.{e.GetTableName()}"
                                : e.GetTableName()
                        ) == tableName
                    );

                if (entityType == null)
                {
                    return NotFound($"Table '{tableName}' not found or not mapped in DbContext.");
                }

                // Extract schema and table name for the SQL query
                string schema = entityType.GetSchema() ?? "dbo"; // Default to dbo if schema is null
                string? actualTableName = entityType.GetTableName();
                if (actualTableName == null)
                {
                    return StatusCode(500, "Table name metadata missing.");
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add(tableName);

                    using (var command = _context.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = $"SELECT * FROM [{schema}].[{actualTableName}]"; // Updated SQL query
                        _context.Database.OpenConnection();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // Add headers
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                worksheet.Cell(1, i + 1).Value = reader.GetName(i);
                            }

                            // Add data
                            int row = 2;
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    worksheet.Cell(row, i + 1).Value = reader
                                        .GetValue(i)
                                        .ToString();
                                }
                                row++;
                            }
                        }
                        _context.Database.CloseConnection();
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"{tableName}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting table '{tableName}' to Excel.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpDelete("delete-row/{tableName}/{id}")]
        public async Task<IActionResult> DeleteRow(string tableName, int id)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Table name cannot be empty.");
            }

            if (tableName != "gameplay.PlayerSessionLog")
            {
                return BadRequest("Deletion is only supported for PlayerSessionLog table.");
            }

            try
            {
                var logEntry = await _context.PlayerSessionLogs.FindAsync(id);
                if (logEntry == null)
                {
                    return NotFound($"Row with ID {id} not found in {tableName}.");
                }

                _context.PlayerSessionLogs.Remove(logEntry);
                await _context.SaveChangesAsync();

                return Ok(
                    new { message = $"Row with ID {id} deleted successfully from {tableName}." }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting row with ID {id} from table {tableName}.");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpPut("update-row/{tableName}/{id}")]
        public async Task<IActionResult> UpdateRow(
            string tableName,
            string id,
            [FromBody] Dictionary<string, object> updatedValues
        )
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return BadRequest("Table name cannot be empty.");
            if (updatedValues == null || updatedValues.Count == 0)
                return BadRequest("No values provided.");

            var entityType = _context
                .Model.GetEntityTypes()
                .FirstOrDefault(e =>
                    (
                        (
                            e.GetSchema() != null
                                ? e.GetSchema() + "." + e.GetTableName()
                                : e.GetTableName()
                        ) == tableName
                    )
                );
            if (entityType == null)
                return NotFound($"Table '{tableName}' not found or not mapped.");

            var pk = entityType.FindPrimaryKey();
            if (pk == null || pk.Properties.Count != 1)
                return BadRequest("Only single-column primary keys are supported.");
            var pkProp = pk.Properties[0];

            object? keyValue;
            try
            {
                keyValue = ConvertToType(id, pkProp.ClrType);
            }
            catch (Exception ex)
            {
                return BadRequest($"Invalid key value: {ex.Message}");
            }

            var clrType = entityType.ClrType;
            object? entity = await FindEntityAsync(clrType, keyValue);
            if (entity == null)
                return NotFound($"Row with key {id} not found in {tableName}.");

            updatedValues.Remove(pkProp.Name);
            updatedValues.Remove("Id");

            foreach (var kvp in updatedValues.ToList())
            {
                var propInfo = clrType.GetProperty(kvp.Key);
                if (propInfo == null || !propInfo.CanWrite)
                    continue;
                try
                {
                    var targetType =
                        Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
                    var converted = kvp.Value == null ? null : ConvertToType(kvp.Value, targetType);
                    propInfo.SetValue(entity, converted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Property conversion failed {Property}", kvp.Key);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = $"Row {id} updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating row {Id} in {Table}", id, tableName);
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        private async Task<object?> FindEntityAsync(Type clrType, object? key)
        {
            try
            {
                var findAsyncTypeMethod = typeof(DbContext)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m =>
                        m.Name == "FindAsync"
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Type)
                    );
                if (findAsyncTypeMethod != null)
                {
                    var vt =
                        (ValueTask<object?>)
                            findAsyncTypeMethod.Invoke(
                                _context,
                                new object?[] { clrType, new object?[] { key } }
                            )!;
                    return await vt;
                }
                var setGeneric = typeof(DbContext)
                    .GetMethod("Set", Type.EmptyTypes)!
                    .MakeGenericMethod(clrType)
                    .Invoke(_context, null);
                var findAsyncGeneric = setGeneric!
                    .GetType()
                    .GetMethod("FindAsync", new[] { typeof(object[]) });
                var vt2 =
                    (ValueTask<object?>)
                        findAsyncGeneric!.Invoke(
                            setGeneric,
                            new object[] { new object?[] { key } }
                        )!;
                return await vt2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reflection lookup failure for {Type}", clrType.Name);
                return null;
            }
        }

        private object? ConvertToType(object value, Type targetType)
        {
            if (value == null)
                return null;
            if (targetType == typeof(string))
                return value.ToString();
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString()!, true);
            if (targetType == typeof(Guid))
                return Guid.Parse(value.ToString()!);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(
                    value.ToString()!,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind
                );
            if (targetType == typeof(bool))
                return value is bool b ? b : bool.Parse(value.ToString()!);
            if (targetType == typeof(int))
                return Convert.ToInt32(value);
            if (targetType == typeof(long))
                return Convert.ToInt64(value);
            if (targetType == typeof(float))
                return Convert.ToSingle(value);
            if (targetType == typeof(double))
                return Convert.ToDouble(value);
            if (targetType == typeof(decimal))
                return Convert.ToDecimal(value);
            // Fallback
            return Convert.ChangeType(value, targetType);
        }

        [HttpGet("table-columns/{tableName}")]
        public IActionResult GetTableColumns(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return BadRequest("Table name cannot be empty.");

            var entityType = _context
                .Model.GetEntityTypes()
                .FirstOrDefault(e =>
                    (
                        (
                            e.GetSchema() != null
                                ? e.GetSchema() + "." + e.GetTableName()
                                : e.GetTableName()
                        ) == tableName
                    )
                );
            if (entityType == null)
                return NotFound($"Table '{tableName}' not found or not mapped.");

            var props = entityType
                .GetProperties()
                .Select(p => new
                {
                    name = p.Name,
                    clrType = p.ClrType.Name,
                    isKey = p.IsPrimaryKey(),
                })
                .OrderByDescending(p => p.isKey) // put key first
                .ThenBy(p => p.name)
                .ToList();
            return Ok(props);
        }

        [HttpPost("create-row/{tableName}")]
        public async Task<IActionResult> CreateRow(
            string tableName,
            [FromBody] Dictionary<string, object> values
        )
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return BadRequest("Table name cannot be empty.");
            if (values == null)
                return BadRequest("No values provided.");

            var entityType = _context
                .Model.GetEntityTypes()
                .FirstOrDefault(e =>
                    (
                        (
                            e.GetSchema() != null
                                ? e.GetSchema() + "." + e.GetTableName()
                                : e.GetTableName()
                        ) == tableName
                    )
                );
            if (entityType == null)
                return NotFound($"Table '{tableName}' not found or not mapped.");

            var pk = entityType.FindPrimaryKey();
            if (pk == null || pk.Properties.Count != 1)
                return BadRequest("Only single-column primary keys are supported.");
            var pkProp = pk.Properties[0];

            object? entity;
            try
            {
                entity = Activator.CreateInstance(entityType.ClrType)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate entity for {Table}", tableName);
                return StatusCode(500, "Entity instantiation failure.");
            }

            // Remove PK if client tried to send it; DB will handle (identity/sequence)
            values.Remove(pkProp.Name);
            values.Remove("Id");

            foreach (var kvp in values.ToList())
            {
                var propInfo = entityType.ClrType.GetProperty(kvp.Key);
                if (propInfo == null || !propInfo.CanWrite)
                    continue;
                try
                {
                    var targetType =
                        Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
                    if (
                        kvp.Value is string s
                        && string.IsNullOrWhiteSpace(s)
                        && targetType != typeof(string)
                    )
                    {
                        propInfo.SetValue(entity, null);
                        continue;
                    }
                    var converted = kvp.Value == null ? null : ConvertToType(kvp.Value, targetType);
                    propInfo.SetValue(entity, converted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Property conversion failed on create {Property}",
                        kvp.Key
                    );
                }
            }

            // Auto-fill non-nullable string props (C# 'required' or DB NOT NULL) if left null/empty
            foreach (var propMeta in entityType.GetProperties())
            {
                if (propMeta.IsPrimaryKey())
                    continue;
                if (propMeta.ClrType != typeof(string))
                    continue;
                // Skip nullable
                if (propMeta.IsNullable)
                    continue;
                var clrProp = entityType.ClrType.GetProperty(propMeta.Name);
                if (clrProp == null || !clrProp.CanWrite)
                    continue;
                var currentVal = clrProp.GetValue(entity) as string;
                if (!string.IsNullOrWhiteSpace(currentVal))
                    continue;
                var lower = propMeta.Name.ToLowerInvariant();
                string generated =
                    lower.Contains("uuid") ? GameServer.Utilities.UserIdUtility.GenerateGuidUserId()
                    : (lower.Contains("hex") || lower.Contains("color")) ? "#FFFFFF"
                    : $"NEW_{DateTime.UtcNow.Ticks}";
                clrProp.SetValue(entity, generated);
            }

            try
            {
                _context.Add(entity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting row into {Table}", tableName);
                var inner = ex.InnerException?.Message;
                return StatusCode(
                    500,
                    $"Insert failed: {ex.Message}{(inner != null ? " | Inner: " + inner : string.Empty)}"
                );
            }

            object? newKeyVal = null;
            try
            {
                var pi = entityType.ClrType.GetProperty(pkProp.Name);
                if (pi != null)
                {
                    newKeyVal = pi.GetValue(entity);
                }
            }
            catch
            { /* ignore */
            }

            return Ok(new { id = newKeyVal });
        }
    }
}
