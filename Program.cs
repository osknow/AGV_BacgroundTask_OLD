using AGV_BackgroundTask.SubPrograms;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AGV_BackgroundTask
{
    class Program
    {
        public static List<GetMissions> tasks_pozagv02;
        public static AGV_SubMachine IpointStatus = new AGV_SubMachine();
        static async Task Main(string[] args)
        {
#if !DEBUG
      
            Console.SetOut(new MyLoger("W:\\BackgroundTasks\\AGV\\logs"));
#endif

            await IPOINT_Sequencer();
            
            await SetResourses();
            // Funkcja aktualizująca zadania przetwarzane przez system AGV.
            await DuniTaskAGV();

            await Main_OpcPaletyzer.Main_2();
        }


        static async Task SetPalletOnIPOINT()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var url = "https://pozagv02.duni.org:1234/api/ResourceAtLocation";
                    client.DefaultRequestHeaders.Add("ApiKey", "C1XUN3agvZ9P2ER");
                    client.DefaultRequestHeaders.Add("Content", "application/json");
                    //
                    var data_palletEuro = new ResourceAtLocation()
                    {
                        symbolicPointId = 4001,
                        resourceType = 3,
                        amount = 1,
                        shelfId = 1
                    };
                    var data_palletAng = new ResourceAtLocation()
                    {
                        symbolicPointId = 4001,
                        resourceType = 1,
                        amount = 1,
                        shelfId = 2
                    };

                    DateTime localDate = DateTime.Now;

                    var response = await client.PostAsJsonAsync(url, data_palletEuro);
                    if (response.IsSuccessStatusCode)
                    {
                        var response_2 = await client.PostAsJsonAsync(url, data_palletAng);   
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }



        static async Task ResetPalletOnIPOINT()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("ApiKey", "C1XUN3agvZ9P2ER");
                    client.DefaultRequestHeaders.Add("Content", "application/json");
                    var url = "https://pozagv02.duni.org:1234/api/ResourceAtLocation";
                    var data_Euro = new ResourceAtLocation()
                    {
                        symbolicPointId = 4001,
                        resourceType = 3,
                        amount = 0,
                        shelfId = 1
                    };

                    var data_Ang = new ResourceAtLocation()
                    {
                        symbolicPointId = 4001,
                        resourceType = 1,
                        amount = 0,
                        shelfId = 2
                    };

                    //var dataJson = JsonSerializer.Serialize(data);

                    DateTime localDate = DateTime.Now;

                    var response = await client.PostAsJsonAsync(url, data_Euro);
                    if ((response.IsSuccessStatusCode)|| !(response.IsSuccessStatusCode))
                    {
                        var response_2 = await client.PostAsJsonAsync(url, data_Ang);
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }

        //**************
        // Miejsce zarządzania sekwencją na podstawie zajętośći miejca IPOINT
        //
        //**************
        static async Task IPOINT_Sequencer()
        {
            bool status_out = false;
            bool status_E_Stop = false;
            bool status_Fault = false;
            bool status_SafetyRelay = false;
            bool status_EndOfMaterial = false;

            try
            {


                var client = new OpcClient("opc.tcp://POZOPC01:5013/POZOPC_IPOINT_AGV");
                client.Connect();
                

                var Place_status = client.ReadNode("ns=3;s=IPOINT_001_AGV.DB_AGV.Place_1 Free");
                var E_Stop = client.ReadNode("ns=3;s=IPOINT_001_AGV.DB_AGV.OWIJARKA E-STOP");
                var Alarm = client.ReadNode("ns=3;s=IPOINT_001_AGV.DB_AGV.OWIJARKA BLAD");
                var SafetyRelay = client.ReadNode("ns=3;s=IPOINT_001_AGV.DB_AGV.OWIJARKA Obwod Bezpieczenstwa");
                var EndOfMaterial = client.ReadNode("ns=3;s=IPOINT_001_AGV.DB_AGV.OWIJARKA koniec foli");
                // FREE     : TRUE
                // OCUPATED : FALSE
                bool status = Convert.ToBoolean(Place_status.Value);

                status_out = status;
                status_E_Stop = Convert.ToBoolean(E_Stop.Value);
                status_Fault = Convert.ToBoolean(Alarm.Value);
                status_SafetyRelay = Convert.ToBoolean(SafetyRelay.Value);
                status_EndOfMaterial = Convert.ToBoolean(EndOfMaterial.Value);
                client.Disconnect();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error:  Błąd odczytania bazy danych OPC.");
                throw;
            }
            if (status_out == true)
            {
                //ResourceAtLocation_Euro();
                //Thread.Sleep(100);
                await ResetPalletOnIPOINT();
            }
            else
            {
                //Miejsce zajęte - zablokować oba miejca dla czytelnośći że zrobił to bacground Task.
                await SetPalletOnIPOINT();
            }

            IpointStatus.E_Stop = status_E_Stop;
            IpointStatus.Fault = status_Fault;
            IpointStatus.EndOfMaterial = status_EndOfMaterial;
            IpointStatus.SafetyRelay = status_SafetyRelay;
            IpointStatus.UpdatedTime = DateTime.Now;

            await PostMachinesToPOZMDA(IpointStatus);
        }

        static async Task<HttpResponseMessage> PostMachinesToPOZMDA(AGV_SubMachine data)
        {
            string HttpSerwerURI = "https://pozmda02.duni.org/api/Agv/AGV_IPOINTStatusUpdate";
            //string HttpSerwerURI = "https://localhost:44396/api/Agv/AGV_IPOINTStatusUpdate";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.PostAsJsonAsync($"{HttpSerwerURI}", data);

                    return response;
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
        static async Task SetResourses()
        {
            using (var client = new HttpClient())
            {
                var url_ResourcesAtLocation = "https://pozagv02.duni.org:1234/api/ResourceAtLocation";
                var url_LoadAtLocation = "https://pozagv02.duni.org:1234/api/LoadAtLocation";

                var jsonserialize = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
                client.DefaultRequestHeaders.Add("ApiKey", "C1XUN3agvZ9P2ER");
                client.DefaultRequestHeaders.Add("Content", "application/json");

                var PSM_038_ENG = new ResourceAtLocation()
                {
                    symbolicPointId = 3001,
                    resourceType = 2,
                    amount = 8,
                    shelfId = -1
                };
                var PSM_038_ENG_Load = new LoadAtLocation()
                {
                    symbolicPointId = 3001
                };

                var PSM_038_EUR = new ResourceAtLocation()
                {
                    symbolicPointId = 2002,
                    resourceType = 4,
                    amount = 8,
                    shelfId = -1
                }; ;
                var PSM_054_PALL = new ResourceAtLocation()
                {
                    symbolicPointId = 2001,
                    resourceType = 4,
                    amount = 8,
                    shelfId = -1
                }; ;

                // First REQUEST
                //var resp = await client.GetAsync(url_LoadAtLocation, PSM_038_ENG_Load);


                var response = await client.PostAsJsonAsync(url_ResourcesAtLocation, PSM_038_EUR);

                DateTime Time = DateTime.Now;
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync();


                    //Console.WriteLine($"{response.StatusCode} | Żądanie paleta EURO  wysłana poprawnie...");
                }
                else
                {
                    Console.WriteLine($"{response.StatusCode} , {response.RequestMessage}");
                }

                // Secound REQUEST  


                var response_2 = await client.PostAsJsonAsync(url_ResourcesAtLocation, PSM_038_ENG);


                if (response_2.IsSuccessStatusCode)
                {
                    string responseString = await response_2.Content.ReadAsStringAsync();
                    
                    //Console.WriteLine($"{response_2.StatusCode} | Żądanie paleta ENG  wysłana poprawnie...");
                }
                else
                {
                    Console.WriteLine($"{response_2.StatusCode} , {response_2.RequestMessage}");
                }

                //Third REQUEST

                var response_3 = await client.PostAsJsonAsync(url_ResourcesAtLocation, PSM_054_PALL);

                
                if (response_3.IsSuccessStatusCode)
                {
                    string responseString = await response_3.Content.ReadAsStringAsync();
                    //Console.WriteLine($"{response_3.StatusCode} | Żądanie palety EURO dla palletyzerów -  wysłana poprawnie...");
                }
                else
                {
                    Console.WriteLine($"{response_3.StatusCode} , {response_3.RequestMessage}");
                }

            }
        }
        
        
        //
        //DUNI TAKS AGV
        //
  
        
        static async Task ChangeTaskStatusByAGV(int status , string duniTaskDetails)
        {
            var url = $"https://pozmda02.duni.org/api/DuniTasks/changeTaskStatusByAGV/{status}/{duniTaskDetails}";

            using (var client = new HttpClient())
            {
                try
                {
                     HttpResponseMessage response = await client.GetAsync(url);


                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(response.StatusCode+" | "+"Zadanie: "+ duniTaskDetails + " zaktualizowane o status: " + status+".");
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }
        static async Task DuniTaskAGV()
        {
            // Lista zadań AGV z pozagv02
            tasks_pozagv02 = await GetMissions_pozagv02.Get();
            //Lista zadań  AGV z pozmda01
            List<GetCurrentTask> tasksNew_pozmda01 = await GetMissions_pozmda01.AGV();
            List<GetCurrentTask> tasksOpen_pozmda01 = new List<GetCurrentTask> { };
            //   
            //Zmiana statusu zadań aktualnie wykonywanych i otrzymanych przez serwer pozagv02

            foreach (GetMissions item_pozagv02 in tasks_pozagv02)
            {
                string item_pozagv02_Id_String = Convert.ToString(item_pozagv02.id);
                foreach (GetCurrentTask item_pozmda01 in tasksNew_pozmda01)
                {
                    //
                    //Lista zadań otwartych / przetwarzanych (Krok 1 lub Krok 2)
                    //
                    if (!(item_pozmda01.statusText=="newTask"))
                    {
                        tasksOpen_pozmda01.Add(item_pozmda01);
                        //
                        bool output_status = false;
                        foreach(var obj in tasks_pozagv02)
                        {
                            string obj_Id_String = Convert.ToString(obj.id);
                            if (((obj.State == "Executing") || (obj.State == "Interrupted")) && (obj_Id_String == item_pozmda01.details))
                            {
                                output_status = true;
                            }
                        }
                        //Kończenie zadań otwartych 
                        if (output_status==false)
                        {
                            await ChangeTaskStatusByAGV(3, item_pozmda01.details);
                            DateTime Time = DateTime.Now;
                            Console.WriteLine($"Zadanie: {item_pozmda01.details} skasowane poprawnie.");
                        }

                        
                    }

                    if (item_pozagv02_Id_String == item_pozmda01.details)
                    {
                        //
                        //Do testów bez przetwarzania zadania niezbędne jest zanegowanie tego warunku
                        //
                        if(item_pozagv02.State== "Executing")
                        {
                            //
                            // Aktualizacja zadania o status "W trakcie"
                            //
                            await ChangeTaskStatusByAGV(1, item_pozagv02_Id_String);
                            //Sprawdzenie ilośći kroków do wykonania w danym zadaniu
                            int length = item_pozagv02.Steps.Count;
                            // 
                            for(int i =0; i<=length-1; i++)
                            {
                                //*  Kroki:
                                //i=0 Pickup
                                //i=1 Dropoff
                                // Narazie na stan 22,09,2023 nie posiadamy więcej kroków przy zadaniu
                                //
                                //
                                //Do testów bez przetwarzania zadania niezbędne jest zanegowanie tego warunku
                                //
                                if (item_pozagv02.Steps[i].StepStatus == "Complete")
                                {
                                    if (i==0)
                                    {
                                        //
                                        //Krok 2 nie zostanie zasygnalizoway ponieważ zadanie znika z listy gdy wykona się osatni krok zadania.
                                        //Tak więc gdy kroków w zadaniu będzie 3 lub więcej wtedy wszystkie do ostatniego będą sygnalizowane.
                                        //
                                        await ChangeTaskStatusByAGV(2, item_pozagv02_Id_String);
                                    }

                                }

                                
                            }

                        }
                    }
                }
            }
        }


        // *************************************************************
        // Funkcja do kasowania WSZYSTKICH zadań 
        // UWAGA
        // NIE UŻYWAĆ !!
        static async Task ClearAllPos()
        {
            List<GetCurrentTask> tasksNew_pozmda01 = await GetMissions_pozmda01.AGV();
            foreach(var obj in tasksNew_pozmda01)
            {
                await ChangeTaskStatusByAGV(3, obj.details);
            }
        }
        // *************************************************************
    }
}
