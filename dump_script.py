import ctypes
from ctypes import c_void_p, c_uint, c_int, c_ushort, c_ulong, c_wchar_p, POINTER, WINFUNCTYPE, Structure, byref

oleaut32 = ctypes.windll.oleaut32

pTypeLib = c_void_p()
oleaut32.LoadTypeLib(c_wchar_p(r'E:\Cut2dx\optimal2dx.dll'), byref(pTypeLib))
vtbl = ctypes.cast(pTypeLib, POINTER(POINTER(c_void_p))).contents
GetTypeInfo = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_uint, POINTER(c_void_p))(vtbl[4])

class ELEMDESC(Structure):
    _fields_ = [('tdesc_p1', c_void_p), ('tdesc_p2', c_void_p), ('paramdesc_p1', c_void_p), ('paramdesc_p2', c_void_p)]

class FUNCDESC(Structure):
    _fields_ = [
        ('memid', c_int),
        ('lprgscode', c_void_p),
        ('lprgelemdescParam', c_void_p),
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

invkinds = {1: 'FUNC', 2: 'PROPGET', 4: 'PROPPUT', 8: 'PROPPUTREF'}

with open('iopt2dx_methods.txt', 'w', encoding='utf-8') as out:
    for ti_idx in range(2):
        pTI = c_void_p()
        GetTypeInfo(pTypeLib, ti_idx, byref(pTI))
        ti_vtbl = ctypes.cast(pTI, POINTER(POINTER(c_void_p))).contents
        
        GetDoc = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_int, POINTER(c_wchar_p), POINTER(c_wchar_p), POINTER(c_ulong), POINTER(c_wchar_p))(ti_vtbl[12])
        ti_name = c_wchar_p()
        GetDoc(pTI, -1, byref(ti_name), None, None, None)
        out.write(f"\n=================== {ti_name.value} ===================\n")
        
        # GetTypeAttr
        class GUID(Structure):
            _fields_ = [('Data1', c_ulong), ('Data2', c_ushort), ('Data3', c_ushort), ('Data4', ctypes.c_ubyte * 8)]
            def __str__(self):
                d4 = ''.join(f'{b:02X}' for b in self.Data4)
                return f'{{{self.Data1:08X}-{self.Data2:04X}-{self.Data3:04X}-{d4[:4]}-{d4[4:]}}}'

        class TYPEATTR(Structure):
            _fields_ = [
                ('guid', GUID),
                ('lcid', c_ulong),
                ('dwReserved', c_ulong),
                ('memidConstructor', c_int),
                ('memidDestructor', c_int),
                ('lpstrSchema', c_wchar_p),
                ('cbSizeInstance', c_ulong),
                ('typekind', c_int),
                ('cFuncs', c_ushort),
                ('cVars', c_ushort),
                ('cImplTypes', c_ushort),
                ('cbSizeVft', c_ushort),
                ('cbAlignment', c_ushort),
                ('wTypeFlags', c_ushort),
                ('wMajorVerNum', c_ushort),
                ('wMinorVerNum', c_ushort),
                ('tdescAlias', c_void_p * 2),
                ('idldescType', c_ulong * 2),
            ]
        GetTIAttr = WINFUNCTYPE(ctypes.HRESULT, c_void_p, POINTER(POINTER(TYPEATTR)))(ti_vtbl[3])
        pAttr = POINTER(TYPEATTR)()
        GetTIAttr(pTI, byref(pAttr))
        cFuncs = pAttr.contents.cFuncs
        ti_vtbl_ReleaseAttr = WINFUNCTYPE(None, c_void_p, POINTER(TYPEATTR))(ti_vtbl[19])
        ti_vtbl_ReleaseAttr(pTI, pAttr)

        GetFuncDesc = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_uint, POINTER(POINTER(FUNCDESC)))(ti_vtbl[5])
        GetNames = WINFUNCTYPE(ctypes.HRESULT, c_void_p, c_int, POINTER(c_wchar_p), c_uint, POINTER(c_uint))(ti_vtbl[7])
        ReleaseFuncDesc = WINFUNCTYPE(None, c_void_p, POINTER(FUNCDESC))(ti_vtbl[20])

        for f in range(cFuncs):
            pFD = POINTER(FUNCDESC)()
            if GetFuncDesc(pTI, f, byref(pFD)) == 0:
                fd = pFD.contents
                rgNames = (c_wchar_p * 32)()
                cNames = c_uint()
                GetNames(pTI, fd.memid, rgNames, 32, byref(cNames))
                names = [rgNames[k] for k in range(cNames.value)]
                inv_str = invkinds.get(fd.invkind, str(fd.invkind))
                func_name = names[0] if names else "<unknown>"
                param_names = ", ".join(names[1:]) if len(names) > 1 else ""
                out.write(f"[{f:03d}] memid={fd.memid:<6} {inv_str:<8} {func_name}({param_names}) [cParams={fd.cParams}]\n")
                ReleaseFuncDesc(pTI, pFD)

print("Finished dumping interfaces to iopt2dx_methods.txt")
