namespace Crest.Members.Services;

/// <summary>
/// The pure graph rules of the reports-to hierarchy: ancestor lists from the edges, the
/// cycle refusal, and every path up. Server-free so the rules are tested alone.
/// </summary>
public static class HierarchyClosure
{
    /// <summary>
    /// The ancestors of every user from the direct managers of each. Managers that have no
    /// entry of their own are tops. Throws when the edges already contain a cycle.
    /// </summary>
    public static Dictionary<string, List<string>> ComputeAncestors(IReadOnlyDictionary<string, List<string>> managersByUser)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userId in managersByUser.Keys)
        {
            Visit(userId);
        }

        return result;

        List<string> Visit(string userId)
        {
            if (result.TryGetValue(userId, out var done))
            {
                return done;
            }

            if (!visiting.Add(userId))
            {
                throw new InvalidOperationException($"The hierarchy contains a cycle through '{userId}'.");
            }

            var ancestors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var managerId in managersByUser.TryGetValue(userId, out var managers) ? managers : [])
            {
                if (seen.Add(managerId))
                {
                    ancestors.Add(managerId);
                }

                foreach (var above in Visit(managerId))
                {
                    if (seen.Add(above))
                    {
                        ancestors.Add(above);
                    }
                }
            }

            visiting.Remove(userId);
            result[userId] = ancestors;
            return ancestors;
        }
    }

    /// <summary>True when making <paramref name="managerId"/> a manager of
    /// <paramref name="reportId"/> would close a cycle: the report is the manager, or is
    /// already above the manager.</summary>
    public static bool WouldCycle(IReadOnlyDictionary<string, List<string>> managersByUser, string reportId, string managerId)
    {
        if (string.Equals(reportId, managerId, StringComparison.Ordinal))
        {
            return true;
        }

        var stack = new Stack<string>([managerId]);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (string.Equals(current, reportId, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var above in managersByUser.TryGetValue(current, out var managers) ? managers : [])
            {
                stack.Push(above);
            }
        }

        return false;
    }

    /// <summary>Every path from a top of the hierarchy down to <paramref name="userId"/>
    /// (top first, the user last), for escalation and approval routing. A user without
    /// managers has the one path of themself.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Chains(IReadOnlyDictionary<string, List<string>> managersByUser, string userId, int maxPaths = 256)
    {
        var paths = new List<IReadOnlyList<string>>();
        Walk(userId, [userId]);
        return paths;

        void Walk(string current, List<string> pathUp)
        {
            if (paths.Count >= maxPaths)
            {
                return;
            }

            var managers = managersByUser.TryGetValue(current, out var list) ? list : [];
            if (managers.Count == 0)
            {
                paths.Add(Enumerable.Reverse(pathUp).ToArray());
                return;
            }

            foreach (var managerId in managers)
            {
                if (pathUp.Contains(managerId, StringComparer.Ordinal))
                {
                    continue;
                }

                Walk(managerId, [.. pathUp, managerId]);
            }
        }
    }
}
