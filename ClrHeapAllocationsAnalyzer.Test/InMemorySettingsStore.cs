using System.Collections.Generic;
using ClrHeapAllocationAnalyzer.Common;

namespace ClrHeapAllocationAnalyzer.Test {
    internal class InMemorySettingsStore : IWritableSettingsStore {
        private readonly IDictionary<string, bool> boolValues = new Dictionary<string, bool>();
        private readonly IDictionary<string, int> intValues = new Dictionary<string, int>();

        public bool GetBoolean(string collectionPath, string propertyName, bool defaultValue)
        {
            return boolValues.ContainsKey(propertyName) ? boolValues[propertyName] : defaultValue;
        }

        public int GetInt32(string collectionPath, string propertyName, int defaultValue)
            {
            return intValues.ContainsKey(propertyName) ? intValues[propertyName] : defaultValue;
        }
        
        public bool CollectionExists(string collectionPath)
        {
            return true;
        }
        
        public void SetBoolean(string collectionPath, string propertyName, bool value)
        {
            boolValues[propertyName] = value;
        }

        public void SetInt32(string collectionPath, string propertyName, int value)
        {
            intValues[propertyName] = value;
        }
        
        public void CreateCollection(string collectionPath)
        { 
        }
    }
}
