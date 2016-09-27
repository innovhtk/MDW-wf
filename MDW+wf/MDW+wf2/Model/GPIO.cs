using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MDW_wf.Model
{
    //[XmlRoot(Namespace = "urn:mdw")]
    public class GPIO
    {
        public string ip { get; set; }
        public bool gpio0 { get; set; }
        public bool gpio1 { get; set; }
        public GPIO()
        {
            ip = "";
            gpio0 = false;
            gpio0 = false;
        }
        public GPIO(string _ip, bool _gpio0, bool _gpio1)
        {
            ip = _ip;
            gpio0 = _gpio0;
            gpio1 = _gpio1;
        }
        public GPIO(string xmlText)
        {
            var stringReader = new System.IO.StringReader(xmlText);
            var serializer = new XmlSerializer(this.GetType());
            GPIO gpio = (GPIO)serializer.Deserialize(stringReader);
            ip = gpio.ip;
            gpio0 = gpio.gpio0;
            gpio1 = gpio.gpio1;
        }
        public string ToXML()
        {
            var stringwriter = new System.IO.StringWriter();
            var serializer = new XmlSerializer(this.GetType());
            serializer.Serialize(stringwriter, this);
            return stringwriter.ToString();
        }


    }
}
