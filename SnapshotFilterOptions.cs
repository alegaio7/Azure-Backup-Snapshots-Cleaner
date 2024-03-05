namespace Azure_Backup_Snapshots_Cleaner
{
    public class SnapshotFilterOptions
    {
        public List<SnapshotFilter>? Filters { get; set; }

        public class SnapshotFilter
        {
            public class Tag
            {
                public string? Name { get; set; }
                public string? Value { get; set; }
                /// <summary>
                /// When true, if a tag is found, the value will not be checked, and will still be considered a match
                /// </summary>
                public bool MatchOnlyWithName { get; set; }
            }

            public int Id { get; set; }
            public string? Description { get; set; }
            public string? StartsWith { get; set; }
            public List<Tag>? Tags { get; set; }
        }
    }
}
