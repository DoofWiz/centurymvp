namespace Century.Core.Contracts
{
    /// <summary>What a save slot holds, for the slot list. Empty slots carry <c>IsEmpty</c>.</summary>
    public struct SaveSlotInfo
    {
        public int Slot;
        public bool IsEmpty;
        public string Title;
        public string Detail;
        public string SavedAt;
    }

    /// <summary>
    /// The save slots, as the campaign layer sees them. The App layer registers the implementation
    /// (it owns the campaign lifecycle and the file system); the title screen and the overmap's
    /// save overlay both talk to this and never to the concrete service.
    /// </summary>
    public interface ISaveSlots
    {
        int SlotCount { get; }

        /// <summary>True while a campaign is running that can be written to a slot.</summary>
        bool CanSaveNow { get; }

        SaveSlotInfo Describe(int slot);

        /// <summary>Writes the running campaign into the slot. False if nothing is running or the write failed.</summary>
        bool Save(int slot);

        /// <summary>Starts the campaign held in the slot. False if the slot is empty or unreadable.</summary>
        bool Load(int slot);

        bool Delete(int slot);
    }
}
