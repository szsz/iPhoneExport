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

        public static string Stringify(this object a)
        {
            if (a is byte[])
            {
                byte[] x = (byte[])a;
                StringBuilder t = new StringBuilder();
                foreach (var item in x)
                {
                    t.Append((char)item);
                }
                return t.ToString();
            }
            return a.ToString();
        }

        public static string TelNumberify(this string s)
        {
            if (s.Contains("@")) // if address is an email then do not format
                return s;
            StringBuilder t = new StringBuilder();
            foreach (var item in s)
            {
                if ((item >= '0' && item <= '9') || item == '+')
                    t.Append(item);
            }
            return t.ToString();
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

        public static string sectotime(int t)
        {
            return string.Format("{0}:{1}:{2}", t / 3600, (t / 60) % 60, t % 60);
        }

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


        static void export(DirectoryInfo max)
        {
            DateTime now = DateTime.Now;
            string pref = max.Name + "_" + now.ToString("yyyy-MM-dd HH.mm.ss");
            using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\31\31bb7ba8914766d4ba40d6dfb6113c8b614be442"))
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
            try
            {
                using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\5a\5a4935c78a5255723f707230a451d79c540d2741"))
                {
                    m_dbConnection.Open();
                    string sql = "select ZADDRESS, ZDATE, ZDURATION, ZORIGINATED from ZCALLRECORD";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    using (StreamWriter sw = new StreamWriter(pref + "_call.csv", false, Encoding.UTF8))
                    {
                        sw.WriteLine("Direction,Date,Duration,Address,LastName,FirstName");
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {

                                string address = reader["ZADDRESS"].Stringify().TelNumberify();
                                Person p = getPersonFromTel(address);
                                //Console.WriteLine("Address: {0}\tDate: {1}\tDuration: {2}\tOrigin {3}\tName: {4} {5} \tOrganization: {6} \tDefinite: {7}", address, UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"), reader["ZDURATION"], reader["ZORIGINATED"], p.firstname, p.lastname, p.organization, definite);
                                sw.WriteLine("{0},{1},{2},{3},{4},{5}",
                                    reader["ZORIGINATED"].ToString() == "1" ? "Outgoing" : "Incoming",
                                    UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"),
                                    sectotime(int.Parse(reader["ZDURATION"].Stringify())),
                                    address.csv(),
                                    p.lastname.csv(),
                                    p.firstname.csv());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.InnerException != null)
                    Console.WriteLine(e.InnerException);
            }

            try
            {
                using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\2b\2b2b0084a1bc3a5ac8c27afdf14afb42c61a19ca"))
                {
                    m_dbConnection.Open();
                    string sql = "select ADDRESS, DATE, DURATION from CALL";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    using (StreamWriter sw = new StreamWriter(pref + "_callsort.csv", false, Encoding.UTF8))
                    {
                        sw.WriteLine("Date,Duration,Address,LastName,FirstName");
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string address = reader["ADDRESS"].Stringify().TelNumberify();
                                Person p = getPersonFromTel(address);
                                //Console.WriteLine("Address: {0}\tDate: {1}\tDuration: {2}\tOrigin {3}\tName: {4} {5} \tOrganization: {6} \tDefinite: {7}", address, UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"), reader["ZDURATION"], reader["ZORIGINATED"], p.firstname, p.lastname, p.organization, definite);
                                sw.WriteLine("{0},{1},{2},{3},{4}",
                                    UnixTimeStampToDateTime(reader.GetDouble(1)).ToString("yyyy-MM-dd HH:mm:ss"),
                                    sectotime(int.Parse(reader["DURATION"].Stringify())),
                                    address.csv(),
                                    p.lastname.csv(),
                                    p.firstname.csv());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.InnerException != null)
                    Console.WriteLine(e.InnerException);
            }

            try
            {
                using (var m_dbConnection = new SQLiteConnection(@"Data Source=" + max.FullName + @"\3d\3d0d7e5fb2ce288813306e4d4636395e047a3d28"))
                {
                    m_dbConnection.Open();
                    string sql = "select text, date, chat_identifier, service_name, is_from_me from chat join chat_message_join on chat.ROWID=chat_message_join.chat_id join message on message.ROWID=chat_message_join.message_id";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    using (StreamWriter sw = new StreamWriter(pref + "_sms.csv", false, Encoding.UTF8))
                    {
                        sw.WriteLine("Direction,Date,Message,Address,LastName,FirstName,Service");
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string address = reader["chat_identifier"].Stringify().TelNumberify();
                                Person p = getPersonFromTel(address);
                                sw.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                                   reader["is_from_me"].Stringify() == "1" ? "Outgoing" : "Incoming",
                                   UnixTimeStampToDateTime(reader.GetDouble(1) + 978307200).ToString("yyyy-MM-dd HH:mm:ss"),
                                   reader["text"].Stringify().csv(),
                                   address.csv(),
                                   p.lastname.csv(),
                                   p.firstname.csv(),
                                   reader["service_name"].Stringify().csv());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (e.InnerException != null)
                    Console.WriteLine(e.InnerException);
            }
        }

        static void Main(string[] args)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Apple Computer\MobileSync\Backup\";
            DirectoryInfo di = new DirectoryInfo(path);
            foreach (var max in di.EnumerateDirectories())
            {
                try
                {
                    export(max);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }


            }
        }
    }
}
