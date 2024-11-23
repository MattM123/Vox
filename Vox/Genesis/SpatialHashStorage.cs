using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Vox.Genesis
{
    public class SpatialHashStorage
    {
        private Dictionary<int[], object> objStore = new();
        public void Add(int[] key, object value)
        {
            objStore.Add(key, value);
        }

        public bool Remove(int[] key)
        {
            return objStore.Remove(key);
        }

        //Custom key logic to handle array keys
        public bool ContainsKey(int[] key)
        {
            foreach (int[] k in objStore.Keys) {
                if (k[0] == key[0] && k[1] == key[1])
                    return true;
            }
            return false;
        }

        public object this[int[] key]
        {
            get => objStore[key];
            set => objStore[key] = value;
        }

        public void Clear()
        {
            objStore.Clear();
        }

        public IEnumerator<KeyValuePair<int[], object>> GetEnumerator()
        {
            return objStore.GetEnumerator();
        }
        
        public int Count()
        {
            return objStore.Count();
        }
        public bool TryGetValue(int[] key, out object value)
        {
            foreach (KeyValuePair<int[], object> kvp in objStore)
            {
                if (kvp.Key[0] == key[0] && kvp.Key[1] == key[1])
                {
                    value = kvp.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public Dictionary<int[], object>.KeyCollection Keys()
        {
            return objStore.Keys;
        }

    }
}
