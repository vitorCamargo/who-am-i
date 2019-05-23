using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Timers;

namespace Server {

    class Program {
        private static Timer timer;
        private static DateTime inicioPartida;

        private static readonly Socket servidorSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<KeyValuePair<string, Socket>> clienteSockets = new List<KeyValuePair<string, Socket>>();

        private const int BUFFER_SIZE = 2048;
        private static int PORT; // GET PELO ARGS[0]
        private static int SEGUNDOS_ANTES_PARTIDA; // GET PELO ARGS[1]
        private static int LIMITE_MAX_SCORE; // GET PELO ARGS[2]

        private static bool partidaInicializada = false;
        private static bool novaPergunta = false;
        private static bool novaPartida = false;
        private static bool fimRodada = false;

        private static Socket mestreJogada;
        private static string resposta;
        private static string dica;

        private static string caminho;

        private static int jogadorVez = -1;

        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        static void Main(string[] args) {
            if(args.Length != 3) {
                Console.WriteLine("Para rodar o servidor é necessário ter os seguintes parâmetros: \nNUMERO_DA_PORTA; \nSEGUNDOS_PARA_COMECAR_PARTIDA; \nQTD_DE_PONTOS_PARA_ENCERRAR_JOGO");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }

            PORT = int.Parse(args[0]);
            SEGUNDOS_ANTES_PARTIDA = int.Parse(args[1]);
            LIMITE_MAX_SCORE = int.Parse(args[2]);

            Console.Title = "Servidor Socket - WhoAmI";
            ConfiguraServidor();

            inicioPartida = DateTime.Now;
            do {
                timer = new Timer();
                timer.Interval = SEGUNDOS_ANTES_PARTIDA * 1000;

                timer.Elapsed += OnTimedEvent;
                timer.AutoReset = false;
                timer.Enabled = true;

                partidaInicializada = false;
                novaPergunta = false;
                novaPartida = false;
                fimRodada = false;

                mestreJogada = null;
                resposta = null;
                dica = null;

                jogadorVez = -1;

                while(!fimRodada);

                Console.WriteLine("\n\nSCORE:\n" + File.ReadAllText(caminho, Encoding.UTF7));
                if(!VerificaFinalJogo()) EnviaMensagemParaTodosProntos(".");
            } while(!VerificaFinalJogo());

            EnviaMensagemParaTodosProntos("* " + File.ReadAllText(caminho, Encoding.UTF7));

            Console.WriteLine("Este Jogo Chegou ao fim, muito obrigado por jogar :)");
            Console.Write("<Digite qualquer coisa para encerrar>: ");
            Console.ReadLine();
            Environment.Exit(0);
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e) {
            partidaInicializada = true;

            foreach(var cliente in clienteSockets) {
                if(cliente.Key == "") {
                    cliente.Value.Shutdown(SocketShutdown.Both);
                    cliente.Value.Close();
                }
            }
            clienteSockets.RemoveAll(item => item.Key.Equals(""));

            if(clienteSockets.Count <= 2) {
                Console.WriteLine("Este Jogo não tem participantes o suficiente para inciar");
                FechaTodosClientes();
                Console.Write("<Digite qualquer coisa para encerrar>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }

            var random = new Random();

            var mestre = clienteSockets[random.Next(clienteSockets.Count)];
            mestreJogada = mestre.Value;

            string textoInicial = "\n\n----------------------------------------------------------------\n";
            textoInicial += "INICIANDO PARTIDA \n\n\n";
            textoInicial += "SCORE ATUAL \n";
            textoInicial += File.ReadAllText(caminho, Encoding.UTF7);
            textoInicial += "----------------------------------------------------------------\n\n";
            textoInicial += "Jogadores participantes: " + clienteSockets.Count + "\n";
            foreach(var elemento in clienteSockets) {
                textoInicial += "\t[" + elemento.Key + "]\n";
            }

            textoInicial += "\nMestre da Rodada: [" + mestre.Key + "]\n";
            textoInicial += "Aguardando definicao de dica e resposta pelo Mestre....\n\n";

            EnviaMensagemParaTodosExcetoMestre(textoInicial);
            EnviaMensagemParaMestre("#MESTRE");

            Console.WriteLine("\nINÍCIO PARTIDA");

            while (resposta == null && dica == null);

            var jogadorAtual = GetJogadorDaVez();

            Console.WriteLine("VEZ: [" + jogadorAtual.Key + "]");

            EnviaMensagemParaTodosExcetoMestre("#INICIA_PARTIDA " + mestre.Key + " " + jogadorAtual.Key + " " + dica);

            do {
                novaPergunta = true;
                jogadorAtual.Value.Send(Encoding.ASCII.GetBytes("#SUA_VEZ_PERGUNTA " + dica));

                while(novaPergunta);
                if(novaPartida) break;

                jogadorAtual = GetJogadorDaVez();
                Console.WriteLine("VEZ: [" + jogadorAtual.Key + "]");

                EnviaMensagemParaTodosExcetoMestreEJogadorVez("#NOVA_PARTIDA " + jogadorAtual.Key);
            } while(!novaPartida);

            EnviaMensagemParaTodosProntos("#ACABOU " + jogadorAtual.Key + " " + SEGUNDOS_ANTES_PARTIDA + " " + resposta);
            fimRodada = true;
        }

        private static void ConfiguraServidor() {
            try {
                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Any, PORT);
                servidorSocket.Bind(ipPoint);
                servidorSocket.Listen(100);
                servidorSocket.BeginAccept(AceitaConexao, null);

                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine(@"SERVIDOR RODANDO EM " + ipPoint.Address.ToString() + ":" + PORT);

                caminho = @"../../scores/" + PORT + ".txt";
                
                if(File.Exists(caminho))
                    File.Delete(caminho);

                using(FileStream fs = File.Create(caminho)) { };

                Console.WriteLine(@"ARQUIVO DE PONTUAÇÃO CRIADO EM: 'scores/" + PORT + ".txt");

                Console.WriteLine("----------------------------------------------------------------\n\n");
            } catch(SocketException) {
                Console.WriteLine("ERRO: Não foi criar um servidor. Verifique a porta utilizada e tente novamente mais tarde.");
                Console.Write("<Digite qualquer coisa para sair>: ");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        private static void FechaTodosClientes() {
            foreach (var cliente in clienteSockets) {
                cliente.Value.Shutdown(SocketShutdown.Both);
                cliente.Value.Close();
            }

            servidorSocket.Close();
        }

        private static string GetNomeCliente(Socket cliente) {
            foreach(var elemento in clienteSockets) {
                if(elemento.Value == cliente)
                    return elemento.Key;
            }

            return "";
        }

        private static KeyValuePair<string, Socket> GetJogadorDaVez() {
            jogadorVez++;

            if (jogadorVez >= clienteSockets.Count)
                jogadorVez = 0;

            if (clienteSockets[jogadorVez].Value == mestreJogada)
                return GetJogadorDaVez();
            
            return clienteSockets[jogadorVez];
        }

        private static void AceitaConexao(IAsyncResult AR) {
            Socket socket;

            try {
                socket = servidorSocket.EndAccept(AR);
            } catch(ObjectDisposedException) {
                return;
            }

            if (!partidaInicializada) {
                clienteSockets.Add(new KeyValuePair<string, Socket>("", socket));

                socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, RecebeDados, socket);
                Console.WriteLine("NOVO CLIENTE LOGADO NO SISTEMA");

                if(clienteSockets.Count == 1)
                    socket.Send(Encoding.ASCII.GetBytes("Atualmente ha so voce conectado. \nPartida inicia em: " + (SEGUNDOS_ANTES_PARTIDA - (DateTime.Now - inicioPartida).Seconds) + " segundos.\n"));
                else
                    socket.Send(Encoding.ASCII.GetBytes("Atualmente ha: " + clienteSockets.Count + " jogadores conectados. \nPartida inicia em: " + (SEGUNDOS_ANTES_PARTIDA - (DateTime.Now - inicioPartida).Seconds) + " segundos.\n"));
            } else {
                socket.Send(Encoding.ASCII.GetBytes("#PARTIDA_INICIALIZADA"));
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            servidorSocket.BeginAccept(AceitaConexao, null);
        }

        private static void RecebeDados(IAsyncResult AR) {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try {
                received = current.EndReceive(AR);
            } catch(SocketException) {
                string nome = GetNomeCliente(current);
                Console.WriteLine("CLIENTE " + nome + " FOI DESCONECTADO.");
                current.Close();
                clienteSockets.RemoveAll(item => item.Value.Equals(current));
                EnviaMensagemParaTodosProntos("[" + nome + "] saiu do jogo..");

                string arquivoTexto = "";
                System.IO.StreamReader arquivo = new System.IO.StreamReader(caminho);

                while(arquivo.Peek() >= 0) {
                    string linha = arquivo.ReadLine();
                    string[] campos = linha.Split(':');

                    if(campos[0] != nome)
                        arquivoTexto += linha + "\r\n";
                }

                arquivo.Close();

                File.WriteAllText(caminho, arquivoTexto, Encoding.UTF7);

                return;
            } catch(ObjectDisposedException) {
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string texto = Encoding.ASCII.GetString(recBuf);

            if(texto.Substring(0, 1) == "@") {
                string comando = texto.Substring(1, texto.IndexOf(" ") - 1);
                string valor = texto.Substring(comando.Length + 2);

                if(comando == "NOME") {
                    Boolean jaExisteNome = false;
                    foreach(var elemento in clienteSockets) {
                        if(elemento.Key == valor) jaExisteNome = true;
                    }

                    if(jaExisteNome) {
                        byte[] textoEnviado = Encoding.ASCII.GetBytes("#ERRO_NOME_JA_UTILIZADO");
                        current.Send(textoEnviado);
                    } else {
                        clienteSockets.RemoveAll(item => item.Value.Equals(current));
                        clienteSockets.Add(new KeyValuePair<string, Socket>(valor, current));
                        byte[] textoEnviado = Encoding.ASCII.GetBytes("#OK");
                        current.Send(textoEnviado);

                        string textoArquivo = File.ReadAllText(caminho, Encoding.UTF7);
                        File.WriteAllText(caminho, textoArquivo + valor + ":0\r\n", Encoding.UTF7);

                        EnviaMensagemParaTodosProntos("[" + valor + "] entrou no jogo..");
                    }
                }
            } else if(texto.Substring(0, 1) == "$") {
                string comando = texto.Substring(1, texto.IndexOf(" ") - 1);
                string valor = texto.Substring(comando.Length + 2);

                if(comando == "DICA") {
                    Console.WriteLine("DICA: " + valor);
                    dica = valor;
                } else if(comando == "RESPOSTA") {
                    Console.WriteLine("RESPOSTA: " + valor);
                    resposta = valor;
                } else if(comando == "PERGUNTA_RESPOSTA") {
                    Console.WriteLine("[MESTRE - " + GetNomeCliente(current) + "] RESPONDEU: " + valor);
                    EnviaMensagemParaTodosExcetoMestreEJogadorVez("[MESTRE]: " + valor);
                    clienteSockets[jogadorVez].Value.Send(Encoding.ASCII.GetBytes("#SUA_VEZ_PALPITE " + valor));
                }
            } else if(texto.Substring(0, 1) == "%") {
                string comando = texto.Substring(1, texto.IndexOf(" ") - 1);
                string valor = texto.Substring(comando.Length + 2);

                if(comando == "PERGUNTA") {
                    Console.WriteLine("[" + GetNomeCliente(current) + "] PERGUNTOU: " + valor);
                    EnviaMensagemParaMestre("#PERGUNTA " + valor);
                    EnviaMensagemParaTodosExcetoMestreEJogadorVez("[" + GetNomeCliente(current) + "]: " + valor);
                } else if(comando == "PALPITE") {
                    if(valor.ToLower() != resposta.ToLower()) {
                        Console.WriteLine("[" + GetNomeCliente(current) + "] ERROU SEU PALPITE");
                        EnviaMensagemParaMestre("\n[" + GetNomeCliente(current) + "] errou seu palpite");
                        EnviaMensagemParaTodosExcetoMestre("\nTentativa: " + valor + "\n[" + GetNomeCliente(current) + "] errou");
                        novaPergunta = false;
                    } else {
                        string arquivoTexto = "";
                        System.IO.StreamReader arquivo = new System.IO.StreamReader(caminho);

                        while(arquivo.Peek() >= 0) {
                            string linha = arquivo.ReadLine();
                            string[] campos = linha.Split(':');

                            if(campos[0] == GetNomeCliente(current))
                                arquivoTexto += campos[0] + ":" + (int.Parse(campos[1]) + 1) + "\r\n";
                            else
                                arquivoTexto += linha + "\r\n";
                        }

                        arquivo.Close();

                        File.WriteAllText(caminho, arquivoTexto, Encoding.UTF7);

                        novaPergunta = false;
                        novaPartida = true;
                    }
                }
            } else {
                if(texto.ToLower() == "sair") {
                    string nome = GetNomeCliente(current);
                    current.Shutdown(SocketShutdown.Both);
                    current.Close();
                    clienteSockets.RemoveAll(item => item.Value.Equals(current));
                    Console.WriteLine("CLIENTE " + nome + " SAIU");

                    string arquivoTexto = "";
                    System.IO.StreamReader arquivo = new System.IO.StreamReader(caminho);

                    while(arquivo.Peek() >= 0) {
                        string linha = arquivo.ReadLine();
                        string[] campos = linha.Split(':');

                        if(campos[0] != nome)
                            arquivoTexto += linha + "\r\n";
                    }

                    arquivo.Close();

                    File.WriteAllText(caminho, arquivoTexto, Encoding.UTF7);

                    return;
                }
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, RecebeDados, current);
        }

        private static void EnviaMensagemParaTodosExcetoMestre(string texto) {
            byte[] textoEnviado = Encoding.ASCII.GetBytes(texto);

            foreach(var cliente in clienteSockets) {
                if(cliente.Key != "" && cliente.Value != mestreJogada) cliente.Value.Send(textoEnviado);
            }
        }

        private static void EnviaMensagemParaTodosExcetoMestreEJogadorVez(string texto) {
            byte[] textoEnviado = Encoding.ASCII.GetBytes(texto);

            foreach(var cliente in clienteSockets) {
                if(cliente.Key != "" && cliente.Value != mestreJogada && cliente.Value != clienteSockets[jogadorVez].Value) cliente.Value.Send(textoEnviado);
            }
        }

        private static void EnviaMensagemParaMestre(string texto) {
            byte[] textoEnviado = Encoding.ASCII.GetBytes(texto);

            mestreJogada.Send(textoEnviado);
        }

        private static void EnviaMensagemParaTodosProntos(string texto) {
            byte[] textoEnviado = Encoding.ASCII.GetBytes(texto);

            foreach(var cliente in clienteSockets) {
                if(cliente.Key != "") cliente.Value.Send(textoEnviado);
            }
        }

        private static bool VerificaFinalJogo() {
            System.IO.StreamReader arquivo = new System.IO.StreamReader(caminho);

            while(arquivo.Peek() >= 0) {
                string linha = arquivo.ReadLine();
                string[] campos = linha.Split(':');

                if (int.Parse(campos[1]) >= LIMITE_MAX_SCORE) {
                    arquivo.Close();
                    return true;
                }
            }

            arquivo.Close();
            return false;
        }
    }
}
