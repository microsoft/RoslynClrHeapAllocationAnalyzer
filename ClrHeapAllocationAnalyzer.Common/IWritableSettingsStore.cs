namespace ClrHeapAllocationAnalyzer.Common {
    /// <summary>
    /// Exposes methods for getting and setting settings values.
    ///
    /// Analogous to the Microsoft.VisualStudio.Settings.WritableSettingsStore,
    /// but with fewer exposed methods.
    /// </summary>
    /// <remarks>
    /// The reason this interface exists is that we do not want to reference
    /// the Visual Studio specific WritableSettingsStore to this project.
    /// </remarks>
    public interface IWritableSettingsStore
    {
        bool GetBoolean(string collectionPath, string propertyName, bool defaultValue);
        int GetInt32(string collectionPath, string propertyName, int defaultValue);
        bool CollectionExists(string collectionPath);
        void SetBoolean(string collectionPath, string propertyName, bool value);
        void SetInt32(string collectionPath, string propertyName, int value);
        void CreateCollection(string collectionPath);
    }
}
