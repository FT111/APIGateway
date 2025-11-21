import requests


url = "http://localhost:5044/users/123"
import threading
threads = []


def make_25_requests(i):
    for _ in range(25):
        response = requests.get(url)
        if response.status_code == 200:
            print(f"Request {i+1}: Success - {response.elapsed.total_seconds()}")
        else:
            print(f"Request {i+1}: Failed with status code {response.status_code}")

for i in range(200):
    thread = threading.Thread(target=make_25_requests, args=(i,))
    threads.append(thread)
    thread.start()

for thread in threads:
    thread.join()

# non threaded version

# for i in range(200):
#     make_25_requests(i)