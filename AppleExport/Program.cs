using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AppleExport
{
    public static class StringHelper
    {
        public static string csv(this string a)
        {
            if (a == null)
                return "";
            a = a.Replace("\r", "");
            a = a.Replace("\n", "\t");
            if (a.Contains(","))
                return "\"" + a + "\"";
            return a;
        }
    }

    class Program
    {

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public struct Person
        {
            public string firstname;
            public string lastname;
            public string organization;
            public string note;
            public bool definite;
        }


        static Dictionary<string, string> guidmap = new Dictionary<string, string>();
        static Dictionary<string, Person> personmap = new Dictionary<string, Person>();

        static Person getPersonFromTel(string tel)
        {
            Person p = new Person();
            p.definite = true;
            string address = tel;
            address = Regex.Replace(address, @"\s+", "");
            string uid = null;
            if (address != "" && address != null)
            {
                if (guidmap.ContainsKey(address))
                    uid = guidmap[address];
                else
                {
                    if (address.Length >= 7)
                    {
                        uid = guidmap.FirstOrDefault(t => { return t.Key.Length >= 7 && t.Key.Substring(t.Key.Length - 7).CompareTo(address.Substring(address.Length - 7)) == 0; }).Value;
                        if (uid != null)
                            p.definite = false;
                    }
                }
                if (uid != null && personmap.ContainsKey(uid))
                    p = personmap[uid];
            }
            return p;
        }

        static void Main(string[] args)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Apple Computer\MobileSync\Backup\";
            DirectoryInfo di = new DirectoryInfo(path);
            DirectoryInfo max = null;
            foreach (var item in di.EnumerateDirectories())
            {
                if(max == null || max.LastWriteTime > item.LastWriteTime)
                {
                    max = item;
                }
            }
            using (var m_dbConnection = new SQLiteConnection(@"Data Source="+max.FullName+@"\31bb7ba8914766d4ba40d6dfb6113c8b614be442"))
            {
                m_dbConnection.Open();
                {
                    string sql = "select value, record_id from ABMultiValue";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //Console.WriteLine("value: " + reader["value"] + "\tguid: " + reader["guid"]);
                            string v = reader["value"].ToString();
                            string uid = reader["record_id"].ToString();
                            v = Regex.Replace(v, @"\s+", "");
                            if (!guidmap.ContainsKey(v))
                                guidmap.Add(v, uid);
                        }
                    }
                }
                {
                    string sql = "select First, Last, Organization, Note, ROWID from ABPerson";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //Console.WriteLine("value: " + reader["value"] + "\tguid: " + reader["guid"]);
                            string uid = reader["ROWID"].ToString();
                            if (!personmap.ContainsKey(uid))
                            {
                                Person p = new Person();
                                p.firstname = reader["First"].ToString();
                                p.lastname = reader["Last"].ToString();
                                p.organization = reader["Organization"].ToString();
                                p.note = reader["Note"].ToString();
                                personmap.Add(uid, p);
                            }
                        }
                    }
                }
            }
            using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\5a4935c78a5255723f707230a451d79c540d2741"))
            {
                m_dbConnection.Open();
                string sql = "select ZADDRESS, ZDATE, ZDURATION, ZORIGINATED from ZCALLRECORD";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                using (StreamWriter sw = new StreamWriter("call.csv", false, Encoding.UTF8))
                {
                    sw.WriteLine("Address,LastName,FirstName,Date,Duration,Direction,Definite");
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string address = reader["ZADDRESS"].ToString();
                            Person p = getPersonFromTel(address);
                            //Console.WriteLine("Address: {0}\tDate: {1}\tDuration: {2}\tOrigin {3}\tName: {4} {5} \tOrganization: {6} \tDefinite: {7}", address, UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"), reader["ZDURATION"], reader["ZORIGINATED"], p.firstname, p.lastname, p.organization, definite);
                            sw.WriteLine("{0},{1},{2},{3},{4},{5},{6}", address.csv(),
                                p.lastname.csv(),
                                p.firstname.csv(),
                                UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"),
                                reader["ZDURATION"].ToString(),
                                reader["ZORIGINATED"].ToString() == "1" ? "OUT" : "IN",
                                p.definite);
                        }
                    }
                }
            }
            using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\3d0d7e5fb2ce288813306e4d4636395e047a3d28"))
            {
                m_dbConnection.Open();
                string sql = "select text, date, chat_identifier, service_name, is_from_me from chat join chat_message_join on chat.ROWID=chat_message_join.chat_id join message on message.ROWID=chat_message_join.message_id";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                using (StreamWriter sw = new StreamWriter("sms.csv", false, Encoding.UTF8))
                {
                    sw.WriteLine("Address,LastName,FirstName,Date,Message,Direction,Service,Definite");
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string address = reader["chat_identifier"].ToString();
                            Person p = getPersonFromTel(address);
                             sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7}", address.csv(),
                                p.lastname.csv(),
                                p.firstname.csv(),
                                UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"),
                                reader["text"].ToString().csv(),
                                reader["is_from_me"].ToString() == "1" ? "OUT" : "IN",
                                reader["service_name"].ToString().csv(),
                                p.definite);
                        }
                    }
                }
            }
        }
    }
}
