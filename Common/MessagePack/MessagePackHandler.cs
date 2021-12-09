﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using MessagePack;
using Rfx.Riken.OsakaUniv.MessagePack;

namespace Rfx.Riken.OsakaUniv
{
    public static class MessagePackDefaultHandler {
        public static T LoadFromFile<T>(string path) {
            T res;
            using (var fs = new FileStream(path, FileMode.Open)) {
                res = LZ4MessagePackSerializer.Deserialize<T>(fs);
            }
            return res;
        }

        public static void SaveToFile<T>(T obj, string path) {
            using (var fs = new FileStream(path, FileMode.Create)) {
                LZ4MessagePackSerializer.Serialize<T>(fs, obj);
            }
        }

        // large list
        public static void SaveLargeListToFile<T>(List<T> obj, string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            {
                LargeListMessagePack.Serialize<T>(fs, obj);
            }
        }

        public static List<T> LoadLargerListFromFile<T>(string path)
        {
            List<T> res;
            using (var fs = new FileStream(path, FileMode.Open))
            {
                res = LargeListMessagePack.Deserialize<T>(fs);
            }
            return res;
        }


    }

    public static class MessagePackMsFinderHandler
    {
        private static string tag = "_bin";

        public static void SaveToFile<T>(T obj, string path)
        {
            var filePath = GetNewFileName(path);
            if (File.Exists(filePath)) File.Move(filePath, filePath + ".backup");
            try {
                MessagePackDefaultHandler.SaveToFile<T>(obj, filePath);
            }
            catch
            {
                
            }
         }

        public static T LoadFromFile<T>(string path)
        {
            var filePath = GetNewFileName(path);
            if (!File.Exists(filePath)) return default(T);
            return MessagePackDefaultHandler.LoadFromFile<T>(filePath);
        }

        private static string GetNewFileName(string path)
        {
            var fileDir = Path.GetDirectoryName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extention = Path.GetExtension(path);
            return fileDir + "\\" + fileName + tag + extention;
        }

    }


    public static class MessagePackHandler {
        const int version = 2;
        public static void SaveToFile<T>(T obj, string path) {
            string newPath;
            if (Path.GetExtension(path).Length > 4)
                newPath = path;
            else
                newPath = path + version;

            if (typeof(T) == typeof(AlignmentResultBean)) {
                AlignmentResultBeanMethods.SaveAlignmentResultBeanToFile((AlignmentResultBean)(object)obj, newPath);
                return;
            }
            else if(typeof(T) == typeof(SavePropertyBean))
            {
                SavePropertyBeanMethods.SaveSavePropertyBeanToFile((SavePropertyBean)(object)obj, newPath);
                return;
            }
            else
            {
                MessagePackDefaultHandler.SaveToFile<T>(obj, newPath);
                return;
            }

            #region old (should be deleted)
            /*
            // check alignment file size (it there are >400 files, AlignedPeakPropertyBean will be saved separately)
            var collection = ((AlignmentResultBean)(object)obj).AlignmentPropertyBeanCollection;
            if (collection == null) return;

            var matrixSize = collection.Count * collection[0].AlignedPeakPropertyBeanCollection.Count;
            if (matrixSize < 1000000) {
                MessagePackDefaultHandler.SaveToFile<T>(obj, newPath);
                return;
            }
            //if (collection.Count < 3000 || collection[0].AlignedPeakPropertyBeanCollection.Count < 400) {
            //    MessagePackDefaultHandler.SaveToFile<T>(obj, newPath);
            //    return;
            //}

            // AlignedPeakPropertyBean file will be saved separately
            var dirPath = Path.GetDirectoryName(newPath) + "\\" + Path.GetFileNameWithoutExtension(newPath) + "_AlignedPeakPropertyBeans";
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            var counter = 0;
            var peakProperty = collection.Select(x => x.AlignedPeakPropertyBeanCollection).ToList();
            foreach (var bean in collection) {
                var filePath = dirPath + "\\AlignedPeakPropertyBean_" + counter + ".arf" + version;
                MessagePackDefaultHandler.SaveToFile<ObservableCollection<AlignedPeakPropertyBean>>(bean.AlignedPeakPropertyBeanCollection, filePath);
                bean.AlignedPeakPropertyBeanCollection = new ObservableCollection<AlignedPeakPropertyBean>();
                counter++;
            }
            MessagePackDefaultHandler.SaveToFile<T>(obj, newPath);
            for(var i = 0; i < peakProperty.Count; i++) {
                collection[i].AlignedPeakPropertyBeanCollection = peakProperty[i];
            }
            */
            #endregion
        }

        public static T LoadFromFile<T>(string path) {
            T res; string newPath;
            newPath = path;
            if (Path.GetExtension(path).Length > 4)
                newPath = path;
            else
                newPath = path + version;

            try {
                res = MessagePackDefaultHandler.LoadFromFile<T>(newPath);
              
                if (typeof(T) == typeof(AlignmentResultBean))
                 {
                    AlignmentResultBeanMethods.LoadAlignmentResultBeanFromFile((AlignmentResultBean)(object)res, newPath);
                    return res;
                }
                else if (typeof(T) == typeof(SavePropertyBean))
                {
                    SavePropertyBeanMethods.LoadSavePropertyBeanFromFile((SavePropertyBean)(object)res, newPath);
                    return res;
                }
            }
            catch (Exception) {
                try {
                    res = (T)LoadFromXmlFile(path, typeof(T));
                }
                catch (Exception) {
                    try {
                        res = (T)LoadFromXmlFile(path.Remove(path.Length), typeof(T));
                    }
                    catch (Exception) {
                        res = default(T);
                    }
                }
                if(res != null)
                    SaveToFile<T>(res, path);
            }                   
              
            return res;
        }

        public static object LoadFromXmlFile(string path, Type type) {
            var serializer = new DataContractSerializer(type);
            var fs = new FileStream(path, FileMode.Open);
            object obj = serializer.ReadObject(fs);
            fs.Close();
            return obj;
        }

    }
}
