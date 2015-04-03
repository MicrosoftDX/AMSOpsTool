using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

//TODO update namespace, as required.
namespace CreateManifestFilter
{
    public class FilterManager
    {
        private const int MaxFilters = 20; //there is no max, but this code needs one to build an xml object array.
        private int _nextFilter = 0;
        private object filterLock = new object();

        public FilterManager()
        {
            filtersField.filter = new filtersFilter[MaxFilters];
        }

        private filters filtersField = new filters();
        public filters ManfiestFilter
        {
            get { return filtersField; }
        }


        public void AddOrUpdateFilter(string filterName, ulong? clipBegin, ulong? clipEnd)
        {
            if (string.IsNullOrEmpty(filterName)) 
                throw new ArgumentException("filterName cannot be empty.");
            if (filterName.Contains(' '))
            {
                throw new ArgumentException("filterName cannot have spaces"); // or special characters
            }

            //look to see if filename exists:
            int thisfilter = MaxFilters;
            int index = 0;
            for (; index < MaxFilters; index++)
            {
                var f = filtersField.filter[index];
                if (f != null && String.Equals(f.name, filterName, StringComparison.CurrentCultureIgnoreCase))
                {
                    //found, we will replace it.
                    thisfilter = index;
                    break;
                }
            }

            if (thisfilter == MaxFilters)
            {
                //not found, increment.
                lock (filterLock)
                {
                    thisfilter = _nextFilter;
                    _nextFilter++;
                }
                if (thisfilter >= MaxFilters)
                    throw new Exception("MaxFilters has been reached.");
            }

            filtersField.filter[thisfilter] = new filtersFilter();
            filtersField.filter[thisfilter].name = filterName;
            filtersField.filter[thisfilter].absTimeInHNS = new filtersFilterAbsTimeInHNS();
            if (clipBegin.HasValue)
            {
                filtersField.filter[thisfilter].absTimeInHNS.ge = clipBegin.Value;
                filtersField.filter[thisfilter].absTimeInHNS.geSpecified = true;
            }
            if (clipEnd.HasValue)
            {
                filtersField.filter[thisfilter].absTimeInHNS.le = clipEnd.Value;
                filtersField.filter[thisfilter].absTimeInHNS.leSpecified = true;
            }

        }

        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }
        public string WriteFilter()
        {
            var serializer = new XmlSerializer(typeof(filters));
            string utf8; //it's still UTF-16 internally, but the XML will be correct
            using (StringWriter writer = new Utf8StringWriter())
            {
                serializer.Serialize(writer, filtersField);
                utf8 = writer.ToString();
            }

            return utf8;
        }

        public void ReadFilter(Stream xmlStream)
        {
            lock (filterLock)
            {
                var serializer = new XmlSerializer(typeof (filters));
                var newfilters = (filters) serializer.Deserialize(xmlStream);

                for (int index = 0; index < MaxFilters; index++)
                {
                    if (index < newfilters.filter.Length)
                    {
                        filtersField.filter[index] = newfilters.filter[index];
                    }
                    else
                    {
                        filtersField.filter[index] = null;
                    }
                }
                _nextFilter = newfilters.filter.Length;
            }
        }

        public void ReadFilter(String xmlString)
        {
            lock (filterLock)
            {
                var sr = new StringReader(xmlString);
                var serializer = new XmlSerializer(typeof (filters));
                var newfilters = (filters)serializer.Deserialize(sr);
                for (int index = 0; index < MaxFilters; index++)
                {
                    if (index < newfilters.filter.Length)
                    {
                        filtersField.filter[index] = newfilters.filter[index];
                    }
                    else
                    {
                        filtersField.filter[index] = null;
                    }
                }
                _nextFilter = newfilters.filter.Length;
            }
        }


    }


    


}