using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Messaging;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;          // очередь сообщений
        private Dictionary<string, MessageQueue> ReturnQueues = new Dictionary<string, MessageQueue>();
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            string path = Dns.GetHostName() + "\\private$\\ServerQueue";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины
            if (MessageQueue.Exists(path))
                q = new MessageQueue(path);
            else
                q = MessageQueue.Create(path);
            // задаем форматтер сообщений в очереди
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });
            // вывод пути к очереди сообщений в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + q.Path;
            // создание потока, отвечающего за работу с очередью сообщений
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        // получение сообщения
        private void ReceiveMessage()
        {
            if (q == null)
                return;
            System.Messaging.Message msg = null;
            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (q.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    msg = q.Receive(TimeSpan.FromSeconds(10.0));

                if (msg.Body.ToString() == "_logout")
                {
                    userList.Invoke((MethodInvoker)delegate
                    {
                        userList.Items.Remove(msg.Label);
                        MessageQueue.Delete(ReturnQueues[msg.Label].Path);
                        ReturnQueues.Remove(msg.Label);
                    });
                }
                else
                {
                    if (!ReturnQueues.ContainsKey(msg.Label))
                    {
                        userList.Invoke((MethodInvoker)delegate
                        {
                            userList.Items.Add(msg.Label);
                            ReturnQueues.Add(msg.Label, new MessageQueue(Dns.GetHostName() + "\\private$\\" + msg.Label));
                        });
                    }
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (msg != null)
                            rtbMessages.Text += "\n >> " + msg.Label + " : " + msg.Body;     // выводим полученное сообщение на форму
                    });
                    //Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                    foreach (string key in ReturnQueues.Keys)
                    {
                        ReturnQueues[key].Send(msg);
                    }
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (q != null)
            {
                //MessageQueue.Delete(q.Path);      // в случае необходимости удаляем очередь сообщений
            }
            foreach (MessageQueue r in ReturnQueues.Values)
            {
                MessageQueue.Delete(r.Path);
            }
        }
    }
}