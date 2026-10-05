namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test helpers that store a blank entry straight away, which the application itself no longer does.</summary>
internal static class EntryServiceTestExtensions
{
    extension(IEntryService service)
    {
        /// <summary>Creates and stores a blank draft.</summary>
        /// <returns>The stored draft.</returns>
        public async Task<DraftEntity> CreateDraft()
        {
            DraftEntity entity = await service.NewDraft();
            await service.InsertDraft(entity);
            return entity;
        }

        /// <summary>Creates and stores a blank note.</summary>
        /// <returns>The stored note.</returns>
        public async Task<NoteEntity> CreateNote()
        {
            NoteEntity entity = await service.NewNote();
            await service.InsertNote(entity);
            return entity;
        }
    }
}
