using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Settings;

namespace ClrHeapAllocationAnalyzer.Test {
    internal class InMemorySettingsStore : WritableSettingsStore {
        private readonly IDictionary<string, bool> boolValues = new Dictionary<string, bool>();
        private readonly IDictionary<string, int> intValues = new Dictionary<string, int>();

        public override bool GetBoolean(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override bool GetBoolean(string collectionPath, string propertyName, bool defaultValue)
        {
            return boolValues.ContainsKey(propertyName) ? boolValues[propertyName] : defaultValue;
        }

        public override int GetInt32(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override int GetInt32(string collectionPath, string propertyName, int defaultValue)
            {
            return intValues.ContainsKey(propertyName) ? intValues[propertyName] : defaultValue;
        }

        public override uint GetUInt32(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override uint GetUInt32(string collectionPath, string propertyName, uint defaultValue)
        {
            throw new NotImplementedException();
        }

        public override long GetInt64(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override long GetInt64(string collectionPath, string propertyName, long defaultValue)
        {
            throw new NotImplementedException();
        }

        public override ulong GetUInt64(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override ulong GetUInt64(string collectionPath, string propertyName, ulong defaultValue)
        {
            throw new NotImplementedException();
        }

        public override string GetString(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override string GetString(string collectionPath, string propertyName, string defaultValue)
        {
            throw new NotImplementedException();
        }

        public override MemoryStream GetMemoryStream(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override SettingsType GetPropertyType(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override bool PropertyExists(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }

        public override bool CollectionExists(string collectionPath)
        {
            return true;
        }

        public override DateTime GetLastWriteTime(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override int GetSubCollectionCount(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override int GetPropertyCount(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> GetSubCollectionNames(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> GetPropertyNames(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override void SetBoolean(string collectionPath, string propertyName, bool value)
        {
            boolValues[propertyName] = value;
        }

        public override void SetInt32(string collectionPath, string propertyName, int value)
        {
            intValues[propertyName] = value;
        }

        public override void SetUInt32(string collectionPath, string propertyName, uint value)
        {
            throw new NotImplementedException();
        }

        public override void SetInt64(string collectionPath, string propertyName, long value)
        {
            throw new NotImplementedException();
        }

        public override void SetUInt64(string collectionPath, string propertyName, ulong value)
        {
            throw new NotImplementedException();
        }

        public override void SetString(string collectionPath, string propertyName, string value)
        {
            throw new NotImplementedException();
        }

        public override void SetMemoryStream(string collectionPath, string propertyName, MemoryStream value)
        {
            throw new NotImplementedException();
        }

        public override void CreateCollection(string collectionPath)
        { 
        }

        public override bool DeleteCollection(string collectionPath)
        {
            throw new NotImplementedException();
        }

        public override bool DeleteProperty(string collectionPath, string propertyName)
        {
            throw new NotImplementedException();
        }
    }
}
