using System;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Client {

    class Program {
        private static readonly Socket ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        private static int PORT;

        static void Main(string[] args) {
            if(args.Length != 1) {
                Console.WriteLine("Para rodar o servidor é necessário ter os seguintes parâmetros: \nNUMERO_DA_PORTA;");
                Console.ReadLine();
                return;
            }

            PORT = int.Parse(args[0]);

            Console.Title = "Cliente Socket - WhoAmI";

            ConectaServidor();
            string mensagem;
            do {
                Partida();
                mensagem = GetRespostaServidor();
            } while(mensagem.Substring(0, 1) != "*");

            Console.Clear();
            Console.WriteLine("Este Jogo Chegou ao fim, muito obrigado por jogar :)");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine(@"SCORE FINAL");
            Console.WriteLine(mensagem.Substring(2));
            Console.WriteLine("----------------------------------------------------------------\n");
            Console.Write("<Digite qualquer coisa para encerrar>: ");
            Console.ReadLine();
            Environment.Exit(0);
        }

        private static void ConectaServidor() {
            int tentativas = 0;

            try {
                tentativas++;
                ClientSocket.Connect(IPAddress.Loopback, PORT);

                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine(@"BEM VINDO AO ""QUEM SOU EU?""");
                Console.WriteLine("----------------------------------------------------------------\n");
                
                string mensagem = GetRespostaServidor();

                if(mensagem != "#PARTIDA_INICIALIZADA") {
                    Console.WriteLine(mensagem);
                    string nome = ""; int tentativa = 0;

                    do {
                        do {
                            if(tentativa == 0)
                                Console.Write("\nPara começar, nos diga: quem é você?\n>>> ");
                            else if(nome.Contains(" "))
                                Console.Write("\nERRO: Sem espaços. \nVamos lá de novo, nos diga: quem é você?\n>>> ");
                            else if(nome == "" && tentativa == 1)
                                Console.Write("\nERRO: Nome Incorreto. \nVamos lá de novo, nos diga: quem é você?\n>>> ");
                            else
                                Console.Write("\nERRO: Nome já está sendo utilizado. \nVamos lá, nos diga: quem é você?\n>>> ");

                            nome = Console.ReadLine();
                            tentativa = 1;
                        } while(nome == "" || nome.Contains(" "));

                        EnviaTextoServidor("@NOME " + nome);
                    } while(GetRespostaServidor() != "#OK");

                    Console.WriteLine("\n\nAguardando inicio da partida........\n");
                } else {
                    Console.WriteLine("ERRO: Partida já iniciada. Tente novamente mais tarde.");
                    Console.Write("<Digite qualquer coisa para sair>: ");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            } catch(SocketException) {
                Console.WriteLine("ERRO: Não foi possível se comunicar com o servidor. Tente novamente mais tarde.");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        private static void Partida() {
            string mensagem = "";
            try {
                do {
                    if(mensagem != "") Console.WriteLine(mensagem);
                    mensagem = GetRespostaServidor();
                } while (mensagem.Substring(0, 1) != "#");
            } catch(ArgumentOutOfRangeException) {
                Console.WriteLine("ERRO: Não foi possível se comunicar com o servidor. Tente novamente mais tarde.");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }

            if(mensagem == "#MESTRE") {
                Console.Clear();
                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine(@"MESTRE DA RODADA");
                Console.WriteLine("----------------------------------------------------------------\n");

                string dica = "", resposta = "";
                do {
                    Console.Write("Informe a dica: \n>>> ");
                    dica = Console.ReadLine();
                } while(dica == "");

                do {
                    Console.Write("\nInforme a resposta: \n>>> ");
                    resposta = Console.ReadLine();
                } while(resposta == "");

                EnviaTextoServidor("$DICA " + dica);
                EnviaTextoServidor("$RESPOSTA " + resposta);

                bool acabou = false;
                do {
                    mensagem = GetRespostaServidor();
                    if(mensagem.Substring(0, 1) != "#") Console.WriteLine(mensagem);
                    else {
                        string comando = mensagem.Substring(1, mensagem.IndexOf(" ") - 1);
                        string valor = mensagem.Substring(comando.Length + 2);

                        if(comando == "PERGUNTA") {
                            string resposta_pergunta = "";
                            Console.WriteLine("\n\nPERGUNTA: '" + valor + "'");
                            do {
                                Console.Write("RESPOSTA (0 - Não; 1 - Sim; Qualquer outro número/letra - Inválido): \n>>> ");
                                resposta_pergunta = Console.ReadLine();
                            } while(resposta_pergunta == "" && resposta_pergunta != "0" && resposta_pergunta != "1");

                            if(resposta_pergunta == "0") resposta_pergunta = "NAO";
                            else if(resposta_pergunta == "1") resposta_pergunta = "SIM";
                            else resposta_pergunta = "PERGUNTA INVALIDA";

                            EnviaTextoServidor("$PERGUNTA_RESPOSTA " + resposta_pergunta);
                        } else if(comando == "ACABOU") {
                            string primeiroValor = mensagem.Substring(mensagem.IndexOf(" ") + 1);
                            string segundoValor = primeiroValor.Substring(primeiroValor.IndexOf(" ") + 1);

                            string vencedor = primeiroValor.Substring(0, primeiroValor.IndexOf(" "));
                            string tempo = segundoValor.Substring(0, segundoValor.IndexOf(" "));
                            string respostaFinal = segundoValor.Substring(segundoValor.IndexOf(" ") + 1);

                            Console.WriteLine("\n\n[" + vencedor + "] ACERTOU A RESPOSTA: " + respostaFinal + " \nPRÓXIMA PARTIDA COMEÇA EM: " + tempo + " SEGUNDOS...");
                            acabou = true;
                        }
                    }
                } while(!acabou);
            } else {
                Console.Clear();
                string primeiroValor = mensagem.Substring(mensagem.IndexOf(" ") + 1);
                string segundoValor = primeiroValor.Substring(primeiroValor.IndexOf(" ") + 1);

                string mestre = primeiroValor.Substring(0, primeiroValor.IndexOf(" "));
                string jogador = segundoValor.Substring(0, segundoValor.IndexOf(" "));
                string dica = segundoValor.Substring(segundoValor.IndexOf(" ") + 1);

                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine(@"PARTIDA INCIADA");
                Console.WriteLine("----------------------------------------------------------------\n");
                Console.WriteLine("MESTRE [" + mestre + "]");
                Console.WriteLine("DICA: '" + dica + "'");
                Console.WriteLine("\n\nINSTRUÇÕES:" +
                    "\n>> Sao válidas apenas perguntas com resposta SIM/NÃO" +
                    "\n>> Perguntas inadequadas ou sem sentido semânticos serao anulados pelo MESTRE" +
                    "\n>> Jogador com pergunta inválida perderá a vez");
                Console.WriteLine("\nJOGADOR DA VEZ: [" + jogador + "]");

                bool acabou = false;
                do {
                    mensagem = GetRespostaServidor();
                    if (mensagem.Substring(0, 1) != "#") Console.WriteLine(mensagem);
                    else {
                        string comando = mensagem.Substring(1, mensagem.IndexOf(" ") - 1);
                        string valor = mensagem.Substring(comando.Length + 2);

                        if (comando == "SUA_VEZ_PERGUNTA") {
                            string pergunta = "";
                            Console.WriteLine("\n\nSUA VEZ...");
                            Console.WriteLine("DICA: '" + valor + "'");
                            do {
                                Console.Write("\nDigite sua pergunta: \n>>> ");
                                pergunta = Console.ReadLine();
                            } while(pergunta == "");

                            EnviaTextoServidor("%PERGUNTA " + pergunta);
                        } else if (comando == "SUA_VEZ_PALPITE") {
                            string palpite = "";
                            Console.WriteLine("\nResposta Mestre: '" + valor + "'");
                            do {
                                Console.Write("Qual seu palpite: \n>>> ");
                                palpite = Console.ReadLine();
                            } while(palpite == "");

                            EnviaTextoServidor("%PALPITE " + palpite);
                        } else if(comando == "NOVA_PARTIDA") {
                            Console.WriteLine("\n\nJOGADOR DA VEZ: [" + valor + "]");
                        } else if(comando == "ACABOU") {
                            primeiroValor = mensagem.Substring(mensagem.IndexOf(" ") + 1);
                            segundoValor = primeiroValor.Substring(primeiroValor.IndexOf(" ") + 1);

                            string vencedor = primeiroValor.Substring(0, primeiroValor.IndexOf(" "));
                            string tempo = segundoValor.Substring(0, segundoValor.IndexOf(" "));
                            string resposta = segundoValor.Substring(segundoValor.IndexOf(" ") + 1);

                            Console.WriteLine("\n\n[" + vencedor + "] ACERTOU A RESPOSTA: " + resposta + " \nPRÓXIMA PARTIDA COMEÇA EM: " + tempo + " SEGUNDOS...");
                            acabou = true;
                        }
                    }
                } while(!acabou);
            }
        }

        private static void SairPartida() {
            EnviaTextoServidor("sair");
            ClientSocket.Shutdown(SocketShutdown.Both);
            ClientSocket.Close();
            Environment.Exit(0);
        }

        private static void EnviaTextoServidor(string texto) {
            try {
                byte[] buffer = Encoding.ASCII.GetBytes(texto);
                ClientSocket.Send(buffer, 0, buffer.Length, SocketFlags.None);
            } catch(SocketException) {
                Console.WriteLine("\n\nVocê foi desconectado do Servidor!");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        private static string GetRespostaServidor() {
            var buffer = new byte[2048];

            try {
                int received = ClientSocket.Receive(buffer, SocketFlags.None);
                if (received == 0) return "";

                var data = new byte[received];
                Array.Copy(buffer, data, received);

                return Encoding.ASCII.GetString(data);
            } catch(SocketException) {
                Console.WriteLine("\n\nVocê foi desconectado do Servidor!");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
                return "";
            }
        }
    }
}
