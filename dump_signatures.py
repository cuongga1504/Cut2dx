import ctypes
from ctypes import c_void_p, c_uint, c_int, c_ushort, c_ulong, c_wchar_p, POINTER, WINFUNCTYPE, Structure, byref

oleaut32 = ctypes.windll.oleaut32

pTypeLib = c_void_p()
oleaut32.LoadTypeLib(c_wchar_p(r'E:\Cut2dx\optimal2dx.dll'), byref(pTypeLib))
vtbl = ctypes.cast(pTypeLib, POINTER(POINTER(c_void_p))).contents
GetTypeInfo = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_uint, POINTER(c_void_p))(vtbl[4])

VARENUM = {
    0: 'void', 1: 'null', 2: 'short', 3: 'int', 4: 'float', 5: 'double',
    6: 'CY', 7: 'DATE', 8: 'BSTR', 9: 'IDispatch*', 10: 'SCODE', 11: 'bool',
    12: 'VARIANT', 13: 'IUnknown*', 14: 'DECIMAL', 16: 'sbyte', 17: 'byte',
    18: 'ushort', 19: 'uint', 20: 'int64', 21: 'uint64', 22: 'int', 23: 'uint',
    24: 'void', 25: 'HRESULT', 26: 'PTR', 27: 'SAFEARRAY', 28: 'CARRAY',
    29: 'USERDEFINED', 30: 'LPSTR', 31: 'LPWSTR', 36: 'RECORD', 37: 'INT_PTR', 38: 'UINT_PTR'
}

class TYPEDESC(Structure):
    pass
TYPEDESC._fields_ = [('p1', c_void_p), ('vt', c_ushort)]

class PARAMDESC(Structure):
    _fields_ = [('pparamdescex', c_void_p), ('wParamFlags', c_ushort)]

class ELEMDESC(Structure):
    _fields_ = [('tdesc', TYPEDESC), ('paramdesc', PARAMDESC)]

class FUNCDESC(Structure):
    _fields_ = [
        ('memid', c_int),
        ('lprgscode', c_void_p),
        ('lprgelemdescParam', POINTER(ELEMDESC)),
        ('funckind', c_int),
        ('invkind', c_int),
        ('callconv', c_int),
        ('cParams', c_ushort),
        ('cParamsOpt', c_ushort),
        ('oVft', c_ushort),
        ('cScodes', c_ushort),
        ('elemdescFunc', ELEMDESC),
        ('wFuncFlags', c_ushort),
    ]

def decode_tdesc(tdesc):
    vt = tdesc.vt
    if vt == 26: # PTR
        sub_tdesc = ctypes.cast(tdesc.p1, POINTER(TYPEDESC)).contents
        return decode_tdesc(sub_tdesc) + '*'
    return VARENUM.get(vt, f'VT_{vt}')

def decode_param_flags(flags):
    res = []
    if flags & 1: res.append('in')
    if flags & 2: res.append('out')
    if flags & 4: res.append('lcid')
    if flags & 8: res.append('retval')
    if flags & 16: res.append('opt')
    return '[' + ', '.join(res) + ']' if res else ''

pTI = c_void_p()
GetTypeInfo(pTypeLib, 0, byref(pTI))
ti_vtbl = ctypes.cast(pTI, POINTER(POINTER(c_void_p))).contents

GetFuncDesc = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_uint, POINTER(POINTER(FUNCDESC)))(ti_vtbl[5])
GetNames = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_int, POINTER(c_wchar_p), c_uint, POINTER(c_uint))(ti_vtbl[7])
ReleaseFuncDesc = WINFUNCTYPE(None, c_void_p, POINTER(FUNCDESC))(ti_vtbl[20])

with open('iopt2dx_signatures.txt', 'w', encoding='utf-8') as out:
    for f in range(131):
        pFD = POINTER(FUNCDESC)()
        if GetFuncDesc(pTI, f, byref(pFD)) == 0:
            fd = pFD.contents
            rgNames = (c_wchar_p * 32)()
            cNames = c_uint()
            GetNames(pTI, fd.memid, rgNames, 32, byref(cNames))
            names = [rgNames[k] for k in range(cNames.value)]
            
            ret_type = decode_tdesc(fd.elemdescFunc.tdesc)
            func_name = names[0] if names else f'Func_{fd.memid}'
            
            params = []
            for p in range(fd.cParams):
                ed = fd.lprgelemdescParam[p]
                ptype = decode_tdesc(ed.tdesc)
                pflags = decode_param_flags(ed.paramdesc.wParamFlags)
                pname = names[p + 1] if p + 1 < len(names) else f'p{p}'
                params.append(f'{pflags} {ptype} {pname}'.strip())
            
            params_str = ", ".join(params)
            out.write(f'{ret_type} {func_name}({params_str});\n')
            ReleaseFuncDesc(pTI, pFD)

print('Written iopt2dx_signatures.txt')
