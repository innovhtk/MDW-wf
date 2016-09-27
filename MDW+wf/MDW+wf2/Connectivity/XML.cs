﻿using HTKLibrary.Classes.MDW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MDW_wf.Connectivity
{
    public class XML<T>
    {
        public XML(string file)
        {
            XmlFile = file;
            ExistsFile = File.Exists(file);
        }

        public bool ExistsFile { get; set; }
        public string XmlFile { get; set; }

        //public static void Write(Tag tag, string path)
        //{

        //}
        //public void Write(Tag tag);
        public T Deserialize()
        {
            if (!ExistsFile) return default (T);
            XmlSerializer deserializer = new XmlSerializer(typeof(T));
            TextReader textReader = new StreamReader(XmlFile);
            T objects;
            objects = (T)deserializer.Deserialize(textReader);
            textReader.Close();

            return objects;
        }
        public List<T> DeserializeList()
        {
            if (!ExistsFile) return new List<T>();
            XmlSerializer deserializer = new XmlSerializer(typeof(List<T>));
            TextReader textReader = new StreamReader(XmlFile);
            List<T> objects;
            objects = (List<T>)deserializer.Deserialize(textReader);
            textReader.Close();

            return objects;
        }
        public void Serialize(List<T> ob)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<T>));
            TextWriter textWriter = new StreamWriter(XmlFile);
            serializer.Serialize(textWriter, ob);
            textWriter.Close();
        }
            
        public void Serialize(T ob)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            TextWriter textWriter = new StreamWriter(XmlFile);
            serializer.Serialize(textWriter, ob);
            textWriter.Close();
        }
        
    }
}
