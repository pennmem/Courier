using System.Collections.Generic;

// using System.Diagnostics;
using UnityEngine;


public class DeliveryItems : MonoBehaviour
{
    [System.Serializable]
    public struct StoreAudio
    {
        public string storeName;
        public AudioClip[] englishAudio;
        public AudioClip[] germanAudio;
    }

    private List<string> unused_store_names = new List<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
    private static List<DeliveryItems> activeDeliveryItemSources = new List<DeliveryItems>();
#endif // UNITY_WEBGL && !UNITY_EDITOR

    private Dictionary<string, List<string>> remainingItems = new Dictionary<string, List<string>>();

    private System.Random reliableRandom;
    public bool isNonDelivery = false;

    public StoreAudio[] storeNamesToItems;
    public StoreAudio[] practiceStoreNamesToItems;

#if !(UNITY_WEBGL && !UNITY_EDITOR) // System.IO
    private static string RemainingItemsPath(string storeName)
    {
        return System.IO.Path.Combine(UnityEPL.GetDataPath(), "remaining_items", storeName);
    }

    private void WriteRemainingItemsFiles()
    {
        foreach (StoreAudio storeAudio in storeNamesToItems)
        {
            string remainingItemsPath = RemainingItemsPath(storeAudio.storeName);
            if (!System.IO.File.Exists(remainingItemsPath))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(remainingItemsPath));
                System.IO.File.Create(remainingItemsPath).Close();
                AudioClip[] languageAudio;
                if (LanguageSource.current_language.Equals(LanguageSource.LANGUAGE.ENGLISH))
                {
                    languageAudio = storeAudio.englishAudio;
                }
                else
                {
                    languageAudio = storeAudio.germanAudio;
                }
                foreach (AudioClip clip in languageAudio)
                {
                    System.IO.File.AppendAllLines(remainingItemsPath, new string[] { clip.name });
                }
            }
        }
    }

    private void WriteAlphabetizedItemsFile()
    {
        string outputFilePath = System.IO.Path.Combine(UnityEPL.GetParticipantFolder(), "wordpool.txt");
        string outputFilePath2 = System.IO.Path.Combine(UnityEPL.GetParticipantFolder(), "all_items.txt");
        List<string> allItems = new List<string>();
        foreach (StoreAudio storeAudio in storeNamesToItems)
        {
            AudioClip[] languageAudio;
            if (LanguageSource.current_language.Equals(LanguageSource.LANGUAGE.ENGLISH))
            {
                languageAudio = storeAudio.englishAudio;
            }
            else
            {
                languageAudio = storeAudio.germanAudio;
            }
            foreach (AudioClip clip in languageAudio)
            {
                allItems.Add(clip.name);
            }
        }
        allItems.Sort();
        System.IO.File.AppendAllLines(outputFilePath, allItems);
        System.IO.File.AppendAllLines(outputFilePath2, allItems);
    }

    private void WriteStoreNamesFile()
    {
        string outputFilePath = System.IO.Path.Combine(UnityEPL.GetParticipantFolder(), "all_stores.txt");
        List<string> allStores = new List<string>();
        foreach (StoreAudio storeAudio in storeNamesToItems)
        {
            allStores.Add(LanguageSource.GetLanguageString(storeAudio.storeName));
        }
        allStores.Sort();
        System.IO.File.AppendAllLines(outputFilePath, allStores);
    }
#endif // !(UNITY_WEBGL && !UNITY_EDITOR)

    void Awake()
    {
        reliableRandom = ReliableRandom();
    #if !(UNITY_WEBGL && !UNITY_EDITOR) // System.IO
        WriteRemainingItemsFiles();
        WriteAlphabetizedItemsFile();
        WriteStoreNamesFile();
    #else
        remainingItems = LoadItems();
        if (!isNonDelivery && !activeDeliveryItemSources.Contains(this))
            activeDeliveryItemSources.Add(this);
    #endif // !(UNITY_WEBGL && !UNITY_EDITOR)

        // Reset pool and populate from configured store entries
        unused_store_names.Clear();
        foreach (StoreAudio storeAudio in storeNamesToItems)
        {
            unused_store_names.Add(storeAudio.storeName);
        }
        // Debug.Log("DeliveryItems Awake: Loaded " + storeNamesToItems.Length + " store entries");

        // for (int i = 0; i < storeNamesToItems.Length; i++)
        // {
        //     Debug.Log($"[{i}] storeName={storeNamesToItems[i].storeName}");
        // }

        // Debug.Log("DeliveryItems Awake: unused_store_names (" + unused_store_names.Count + " total):");
        // foreach (string name in unused_store_names)
        // {
        //     Debug.Log(" - " + name);
        // }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    void OnDestroy()
    {
        activeDeliveryItemSources.Remove(this);
    }
#endif // UNITY_WEBGL && !UNITY_EDITOR
    // void Awake()
    // {
    //     reliableRandom = ReliableRandom();

    // #if !UNITY_WEBGL // System.IO
    //     WriteRemainingItemsFiles();
    //     WriteAlphabetizedItemsFile();
    //     WriteStoreNamesFile();
    // #else
    //     remainingItems = LoadItems();
    // #endif

    //     // Gather non-delivery store names (like post_office)
    //     List<string> nonDeliveryNames = new List<string>();
    //     if (nonDeliveryItems != null)
    //     {
    //         foreach (StoreAudio nonDeliveryStore in nonDeliveryItems.storeNamesToItems)
    //         {
    //             nonDeliveryNames.Add(nonDeliveryStore.storeName);
    //         }
    //     }

    //     // Add only delivery stores
    //     foreach (StoreAudio storeAudio in storeNamesToItems)
    //     {
    //         if (!nonDeliveryNames.Contains(storeAudio.storeName))
    //         {
    //             unused_store_names.Add(storeAudio.storeName);
    //         }
    //         else
    //         {
    //             Debug.Log($"[DeliveryItems] Excluding non-delivery store: {storeAudio.storeName}");
    //         }
    //     }

    //     Debug.Log("DeliveryItems Awake: Loaded " + storeNamesToItems.Length + " store entries");
    //     Debug.Log("DeliveryItems Awake: " + unused_store_names.Count + " stores added to delivery pool");
    // }


    private Dictionary<string, List<string>> LoadItems()
    {
        Dictionary<string, List<string>> allItems = new Dictionary<string, List<string>>();
        foreach (StoreAudio store in storeNamesToItems)
        {
            allItems.Add(store.storeName, new List<string>());

            foreach (AudioClip clip in GetLanguageAudio(store))
            {
                allItems[store.storeName].Add(clip.name);
            }
        }
        return allItems;
    }

    public string PopStoreName()
    {
        // Debug.Log($"PopStoreName (isNonDelivery={isNonDelivery}) - scene store count: {FindObjectsOfType<StoreComponent>().Length}, names available: {unused_store_names.Count}");

        if (unused_store_names.Count < 1)
        {
            throw new UnityException("I ran out of store names!");
        }


        unused_store_names.Shuffle(reliableRandom);
        string storeName = unused_store_names[0];
        unused_store_names.RemoveAt(0);

        // Debug.Log($"PopStoreName - popped: {storeName}; remaining: {unused_store_names.Count}");

        return storeName;
    }

    public bool StoresSetup()
    {
        // Debug.Log($"StoresSetup (isNonDelivery={isNonDelivery}): {unused_store_names.Count} stores left");
        return unused_store_names.Count == 0;
    }

    // This assumes that the item is ONLY in the practice list and NOT in the main list!
    // This does not remove the item from the remainingItems file
    public AudioClip UsePracticeItem(string storeName, string itemName) 
    {
        //get the item
        StoreAudio storeAudio = System.Array.Find(practiceStoreNamesToItems, 
                                                  store => store.storeName.Equals(storeName));
        if (storeAudio.storeName == null)
            throw new UnityException("I couldn't find the store: " + storeName);
        
        AudioClip[] languageAudioClips;
        if (LanguageSource.current_language.Equals(LanguageSource.LANGUAGE.ENGLISH))
            languageAudioClips = storeAudio.englishAudio;
        else
            languageAudioClips = storeAudio.germanAudio;

        AudioClip item = System.Array.Find(languageAudioClips, 
                                           clip => clip.name.Equals(itemName));
        if (item == null)
            throw new UnityException("Possible language mismatch. I couldn't find: " + itemName + " in " + storeName);

        return item;
    }

    public AudioClip PopItem(string storeName)
    {
        #if !(UNITY_WEBGL && !UNITY_EDITOR) // System.IO
            // Get the item
            string remainingItemsPath = RemainingItemsPath(storeName);
            string[] remainingItems = System.IO.File.ReadAllLines(remainingItemsPath);
            int randomItemIndex = Random.Range(0, remainingItems.Length);
            string randomItemName = remainingItems[randomItemIndex];
            
            StoreAudio storeAudio = System.Array.Find(storeNamesToItems,
                                                    store => store.storeName.Equals(storeName));
            if (storeAudio.storeName == null)
                throw new UnityException("I couldn't find the store: " + storeName);
            
            AudioClip[] languageAudioClips;
            if (LanguageSource.current_language.Equals(LanguageSource.LANGUAGE.ENGLISH))
                languageAudioClips = storeAudio.englishAudio;
            else
                languageAudioClips = storeAudio.germanAudio;
            
            AudioClip randomItem = System.Array.Find(languageAudioClips, 
                                                    clip => clip.name.Equals(randomItemName));
            if (randomItem == null)
                throw new UnityException("Possible language mismatch. I couldn't find an item for: " + storeName);

            // Delete it from remaining items
            System.Array.Copy(remainingItems, randomItemIndex + 1, 
                            remainingItems, randomItemIndex, 
                            remainingItems.Length - randomItemIndex - 1);
            System.Array.Resize(ref remainingItems, remainingItems.Length - 1);
            System.IO.File.WriteAllLines(remainingItemsPath, remainingItems);
            // Debug.Log("Items remaining: " + remainingItems.Length.ToString());
        #else
            if (!remainingItems.ContainsKey(storeName))
                throw new UnityException("I couldn't find the store: " + storeName);
            if (remainingItems[storeName].Count == 0)
                throw new UnityException("I ran out of items for store: " + storeName);

            int randomItemIndex = UnityEngine.Random.Range(0, remainingItems[storeName].Count);
            string randomItemName = remainingItems[storeName][randomItemIndex];

            StoreAudio storeAudio = System.Array.Find(storeNamesToItems,
                                                      store => store.storeName.Equals(storeName));
            if (storeAudio.storeName == null)
                throw new UnityException("I couldn't find the store: " + storeName);

            AudioClip randomItem = System.Array.Find(GetLanguageAudio(storeAudio),
                                                     clip => clip.name.Equals(randomItemName));
            if (randomItem == null)
                throw new UnityException("Possible language mismatch. I couldn't find an item for: " + storeName);

            remainingItems[storeName].Remove(randomItemName);
        #endif // !(UNITY_WEBGL && !UNITY_EDITOR)
        
        //return the item
        return randomItem;
    }

    public static bool ItemsExhausted()
    {
        #if !(UNITY_WEBGL && !UNITY_EDITOR) // System.IO
            bool itemsExhausted = false;
            string remainingItemsDirectory = RemainingItemsPath("");
            if (!System.IO.Directory.Exists(remainingItemsDirectory))
                return false;
            string[] remainingItemsPaths = System.IO.Directory.GetFiles(remainingItemsDirectory);
            foreach (string remainingItemsPath in remainingItemsPaths)
            {
                string[] itemsRemaining = System.IO.File.ReadAllLines(remainingItemsPath);
                bool storeExhausted = itemsRemaining.Length == 0;
                if (storeExhausted)
                    itemsExhausted = true;
            }
            return itemsExhausted;
        #else
            foreach (DeliveryItems itemSource in activeDeliveryItemSources)
            {
                foreach (List<string> storeItems in itemSource.remainingItems.Values)
                {
                    if (storeItems.Count == 0)
                        return true;
                }
            }

            return false;
        #endif // !(UNITY_WEBGL && !UNITY_EDITOR)
    }

    private AudioClip[] GetLanguageAudio(StoreAudio storeAudio)
    {
        if (LanguageSource.current_language.Equals(LanguageSource.LANGUAGE.ENGLISH))
            return storeAudio.englishAudio;
        return storeAudio.germanAudio;
    }

    public System.Random ReliableRandom()
    {
        string participantCode = UnityEPL.GetParticipants()[0];
        string[] codeParts = participantCode.Split('_');
        return new System.Random(codeParts[0].GetHashCode());
    }

    
}
