import os
import time
import json
import socket
import psutil
import distro
from datetime import datetime
from datetime import timedelta
import pika
import logging

logging.basicConfig(level=logging.INFO)

def get_system_info():
    IP = socket.gethostbyname(socket.gethostname())
    OperatingSystem = " ".join(distro.linux_distribution())
    HostName = socket.gethostname()
    TimeStamp = getLocalTimeString()
    CPULoad = str(psutil.cpu_percent(interval=1))
    memory = psutil.virtual_memory()
    UsedMemory = memory.used / (1024 ** 3)
    TotalMemory = memory.total / (1024 ** 3)
    UserName = os.getenv('USER', 'unknown')
    UpTime = getUptimeString()

    return {
        "IP": IP,
        "OperatingSystem": OperatingSystem,
        "HostName": HostName,
        "TimeStamp": TimeStamp,
        "CPULoad": CPULoad,
        "UsedMemory": UsedMemory,
        "TotalMemory": TotalMemory,
        "UserName": UserName,
        "UpTime": UpTime
    }

def getLocalTimeString():
    timestamp = datetime.now()

    # Format the timestamp as a string in the desired format
    timestamp_str = timestamp.strftime("%Y-%m-%dT%H:%M:%S")
    
    return timestamp_str

def getUptimeString():
    # Calculate the uptime in seconds
    uptime_seconds = time.time() - psutil.boot_time()

    # Convert the uptime to a timedelta object
    uptime_timedelta = timedelta(seconds=uptime_seconds)

    # Format the timedelta to the desired string format "hh:mm:ss.ffffff"
    uptime_str = str(uptime_timedelta)
    
    return uptime_str

def send_to_rabbitmq(data):
    credentials = pika.PlainCredentials(
        os.getenv('RABBITMQ_USER', 'admin'),
        os.getenv('RABBITMQ_PASS', 'root0603')
    )
    parameters = pika.ConnectionParameters(
        host=os.getenv('RABBITMQ_HOST', 'localhost'),
        port=int(os.getenv('RABBITMQ_PORT', 5672)),
        virtual_host=os.getenv('RABBITMQ_VHOST', '/'),
        credentials=credentials
    )
    
    max_retries = 5
    for attempt in range(max_retries):
        try:
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()
            
            channel.queue_declare(queue='monitor_service_queue')
            
            channel.basic_publish(exchange='',
                                  routing_key='monitor_service_queue',
                                  body=json.dumps(data))
            
            connection.close()
            logging.info("Successfully sent data to RabbitMQ")
            
            return
        except pika.exceptions.AMQPConnectionError as e:
            logging.error(f"Failed to connect to RabbitMQ (attempt {attempt+1}/{max_retries}): {str(e)}")
            time.sleep(5)
    
    logging.error("Failed to connect to RabbitMQ after multiple attempts")

def run_service():
    while True:
        try:
            system_info = get_system_info() # Read the system info
            send_to_rabbitmq(system_info) # Enqueue it with rabbitMQ
        except Exception as e:
            logging.error(f"Error in run_service: {str(e)}")
        time.sleep(5)  # Wait for 5 seconds before the next update

if __name__ == "__main__":
    logging.info("Starting system info service")
    run_service()