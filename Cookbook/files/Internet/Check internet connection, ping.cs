/// To check Internet connection, try to connect to a known reliable Internet server. For it can be used ICMP ping.

bool canConnectToGoogle = internet.ping();
print.it(canConnectToGoogle);
