local ffi = require 'ffi'
ffi.cdef[[
typedef struct {
    void* data; // hiMQMemory + 32Byte
    void* end;
    void* header;
    void* current; // read/write position
} hiMQInstance;
hiMQInstance* hiMQ_createIPC(uint32_t size);
hiMQInstance* hiMQ_create(void* ptr, uint32_t size);
const char* hiMQ_getIPCName(hiMQInstance* inst);
hiMQInstance* hiMQ_openIPC(const char* name);
hiMQInstance* hiMQ_open(void* ptr, uint32_t size);
void hiMQ_closeIPC(hiMQInstance* inst);
void hiMQ_close(hiMQInstance* inst);
uint32_t hiMQ_wait(hiMQInstance* inst, uint32_t ms);
uint32_t hiMQ_get(hiMQInstance* inst);
uint32_t hiMQ_next(hiMQInstance* inst);
void hiMQ_begin(hiMQInstance* inst);
uint32_t hiMQ_ensure(hiMQInstance* inst, uint32_t size);
void hiMQ_end(hiMQInstance* inst, uint32_t size, uint32_t setEvent);
]]
return hdtLoadFFI('huMessageQueue.dll')
