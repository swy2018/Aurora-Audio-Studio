namespace AuroraAudioStudio.Services;

public static class SettingsPathValidator
{
    public static bool TryValidate(string localAiRoot, string outputRoot, string projectsRoot, out string error)
    {
        foreach (var (name, value) in new[]
        {
            ("模型目录", localAiRoot),
            ("成品目录", outputRoot),
            ("处理记录目录", projectsRoot)
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"{name}不能为空。";
                return false;
            }

            try
            {
                var full = Path.GetFullPath(value.Trim());
                if (!Path.IsPathFullyQualified(full) || string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase))
                {
                    error = $"{name}不能直接使用磁盘根目录。";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"{name}无效：{ex.Message}";
                return false;
            }
        }

        error = "";
        return true;
    }
}
