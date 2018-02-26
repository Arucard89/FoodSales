using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using FoodSales.Models;
using System.Text;
using System.DirectoryServices;
using System.Security.Claims;
using System.Security.Principal;
using System.Web.UI;

namespace FoodSales.Controllers
{
    public class HomeController : Controller
    {
        static DirectoryEntry createDirectoryEntry()
        {
            DirectoryEntry ldapConnection = new DirectoryEntry("gt.npo-saturn.int");
            ldapConnection.Path = "LDAP://dc=gt,dc=npo-saturn,dc=int";
            ldapConnection.AuthenticationType = AuthenticationTypes.Secure;
            return ldapConnection;
        }
        static string GetProperty(SearchResult searchResult, string PropertyName)
        {
            if (searchResult.Properties.Contains(PropertyName))
            {
                return searchResult.Properties[PropertyName][0].ToString();
            }
            else
            {
                return string.Empty;
            }
        }
        private string GetInfoFromAD(string userName, string prop = "employeeID") //получаем информацию из AD(по умолчанию ИД) по логину
        {
            // create and return new LDAP connection with desired settings  
            string inf;
            try
            {
                DirectoryEntry myLdapConnection = createDirectoryEntry();
                DirectorySearcher search = new DirectorySearcher(myLdapConnection);
                search.Filter = "(sAMAccountName=" + userName + ")";
                inf = GetProperty(search.FindOne(), prop);
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка: " + e.Message);
                inf = "";
            }
            return inf;
        }

        void ShowAlert(string message)
        {
            ViewBag.AlertStyle = "";
            ViewBag.AlertMessage = message;
        }

        void HideAlert()
        {
            ViewBag.AlertStyle = "display: none";
        }

        void HideMessage()
        {
            ViewBag.InfoStyle = "display: none";
        }

        void ShowInfo(string message)
        {
            ViewBag.InfoStyle = "";
            ViewBag.InfoMessage = message;
        }
        //[Authorize(Users = @"gt\domain users")]
        public ActionResult Index(string usr="", string userDate1 = "", string userDate2 = "")
        {
            string userLogin;
            HideAlert();
            if (string.IsNullOrEmpty(usr))
            {
                userLogin = System.Web.HttpContext.Current.User.Identity.Name;
            }
            else
            {
                userLogin = usr;
            }
            userLogin = userLogin.ToLower().Replace("gt\\", "");

            ViewBag.card = "";//если карта не выдана, то строка не меняется
            HideMessage();//скрываем информационное сообщение

            //если не определили пользователя
            //ViewBag.User = User;
            ViewBag.userLogin = userLogin;
            if (String.IsNullOrEmpty(userLogin))
            {
                ShowAlert("Пользователь не определен");
                return View();
            }
            
            //получаем пользователя
            string EmplID = GetInfoFromAD(userLogin);
            string FIO = GetInfoFromAD(userLogin, "sn");
            if (String.IsNullOrEmpty(FIO))
            {
                ShowAlert("Ошибка учетной записи");
                return View();
            }
            //выводим ФИО
            ViewBag.FIO = FIO;
            //ищем карту и проверяем статус
            foodsalesEntities db = new foodsalesEntities();
            CardOwner card = db.CardOwners.Where(p => p.PersonID == EmplID).FirstOrDefault();
            if (card == null)
            {
                ShowAlert("Карта пользователя не найдена");
                return View();
            }

            //if (card.CardStatus.ToLower() != "выдана")
            //{
            //    ShowAlert("Статус карты: " + card.CardStatus);
            //}
            ViewBag.CardStatus = "Статус карты: " + card.CardStatus;

            if (card.CardStatus.ToLower() == "выдана")     //если выдана карта, то включаем отображение
            {
                ViewBag.card ="<span class=\"glyphicon glyphicon-credit-card\" data-toggle=\"tooltip\" title=\"" + card.CardID + "\"></span>";
            }
           
            

            DateTime date1;
            DateTime date2;
            bool dataFlag1 = false, dataFlag2 = false;
            DateTime now = DateTime.Now;
            DateTime dateAct;

            //определяем дату актуальных данных для вывода в сообщение
            if (now < new DateTime(now.Year, now.Month, now.Day, 10, 00, 00))
            {
                dateAct = now.AddDays(-2);
            }
            else
            {
                dateAct = now.AddDays(-1);
            }
            switch (now.DayOfWeek)// в понедельник никто не обновляет, поэтому:
            {
                case DayOfWeek.Monday:
                    dateAct = dateAct.AddDays(-2);
                    break;
            }
            ViewBag.dateAct = dateAct.ToShortDateString();
            //проверяем даты
            if (String.IsNullOrEmpty(userDate1))//ставим начало месяца, если нет даты начала
            {
                date1 = new DateTime(now.Year, now.Month, 1);
                dataFlag1 = true;
            }
            else //иначе проверяем правильность ввода
            {
                dataFlag1 = DateTime.TryParse(userDate1, out date1);
            }
            if (String.IsNullOrEmpty(userDate1))//ставим начало месяца, если нет даты конца
            {
                date2 = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);
                dataFlag2 = true;
            }
            else //иначе проверяем правильность ввода
            {
                dataFlag2 = DateTime.TryParse(userDate2, out date2);
                if (dataFlag2)//делаем конец дня
                {
                    date2 = new DateTime(date2.Year, date2.Month, date2.Day, 23, 59, 59);
                }
            }

            //Если неправильные даты, то выводим сообщение
            if (!dataFlag1 || !dataFlag2)
            {
                Console.WriteLine("Неверный формат даты");
                Console.WriteLine(userDate1);
                Console.WriteLine(userDate2);
                ShowAlert("Неверный формат даты");
                return View();
            }
            if (date1 > date2)
            {
                ShowAlert("Период выбран неверно");
                return View();
            }
           
            IEnumerable<Sale> sales = db.Sales.Where(s => ((s.CardID == card.CardID) && (s.Period.CompareTo(date1) >= 0) && (s.Period.CompareTo(date2) <= 0))).OrderBy(s=>s.Period).AsEnumerable();
            //показываем информацию о недоступности данных
            if (sales.Count() <= 0)
            {
                ShowInfo("За выбранный период данные отсутствуют");
            }
            
            ViewBag.sales = sales;
            decimal salesSum = sales.Select(s => s.Summa).Sum();
            ViewBag.salesSum = salesSum;
            decimal moneyRest = 1890 - salesSum;
            ViewBag.moneyRest = moneyRest;

            ViewBag.date1 = date1.ToString("dd.MM.yyyy");
            ViewBag.date2 = date2.ToString("dd.MM.yyyy");

            return View();
        }
    }
}