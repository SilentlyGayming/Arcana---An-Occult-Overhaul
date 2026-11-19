"""Reads CLR metadata and method bodies so the build can check the native IL contracts Arcana relies on."""
import struct, sys

OPS = {}
def op(code, name, arg): OPS[code] = (name, arg)
one = ["nop","break","ldarg.0","ldarg.1","ldarg.2","ldarg.3","ldloc.0","ldloc.1","ldloc.2","ldloc.3","stloc.0","stloc.1","stloc.2","stloc.3"]
for i, n in enumerate(one): op(i, n, 0)
op(0x0E,"ldarg.s",1);op(0x0F,"ldarga.s",1);op(0x10,"starg.s",1);op(0x11,"ldloc.s",1);op(0x12,"ldloca.s",1);op(0x13,"stloc.s",1)
op(0x14,"ldnull",0)
for i in range(9): op(0x16+i, "ldc.i4."+str(i), 0)
op(0x15,"ldc.i4.m1",0);op(0x1F,"ldc.i4.s",1);op(0x20,"ldc.i4",4);op(0x21,"ldc.i8",8);op(0x22,"ldc.r4","r4");op(0x23,"ldc.r8",8)
op(0x25,"dup",0);op(0x26,"pop",0);op(0x27,"jmp","tok");op(0x28,"call","tok");op(0x29,"calli","tok");op(0x2A,"ret",0)
for i,n in enumerate(["br.s","brfalse.s","brtrue.s","beq.s","bge.s","bgt.s","ble.s","blt.s","bne.un.s","bge.un.s","bgt.un.s","ble.un.s","blt.un.s"]): op(0x2B+i,n,1)
for i,n in enumerate(["br","brfalse","brtrue","beq","bge","bgt","ble","blt","bne.un","bge.un","bgt.un","ble.un","blt.un"]): op(0x38+i,n,4)
op(0x45,"switch","sw")
for i,n in enumerate(["ldind.i1","ldind.u1","ldind.i2","ldind.u2","ldind.i4","ldind.u4","ldind.i8","ldind.i","ldind.r4","ldind.r8","ldind.ref","stind.ref","stind.i1","stind.i2","stind.i4","stind.i8","stind.r4","stind.r8","add","sub","mul","div","div.un","rem","rem.un","and","or","xor","shl","shr","shr.un","neg","not","conv.i1","conv.i2","conv.i4","conv.i8","conv.r4","conv.r8","conv.u4","conv.u8"]): op(0x46+i,n,0)
op(0x6F,"callvirt","tok");op(0x70,"cpobj","tok");op(0x71,"ldobj","tok");op(0x72,"ldstr","tok");op(0x73,"newobj","tok");op(0x74,"castclass","tok");op(0x75,"isinst","tok");op(0x76,"conv.r.un",0);op(0x79,"unbox","tok");op(0x7A,"throw",0)
op(0x7B,"ldfld","tok");op(0x7C,"ldflda","tok");op(0x7D,"stfld","tok");op(0x7E,"ldsfld","tok");op(0x7F,"ldsflda","tok");op(0x80,"stsfld","tok");op(0x81,"stobj","tok")
for i in range(0x82,0x8C): op(i,"conv.ovf.x",0)
op(0x8C,"box","tok");op(0x8D,"newarr","tok");op(0x8E,"ldlen",0);op(0x8F,"ldelema","tok")
for i,n in enumerate(["ldelem.i1","ldelem.u1","ldelem.i2","ldelem.u2","ldelem.i4","ldelem.u4","ldelem.i8","ldelem.i","ldelem.r4","ldelem.r8","ldelem.ref","stelem.i","stelem.i1","stelem.i2","stelem.i4","stelem.i8","stelem.r4","stelem.r8","stelem.ref"]): op(0x90+i,n,0)
op(0xA3,"ldelem","tok");op(0xA4,"stelem","tok");op(0xA5,"unbox.any","tok")
for i in range(0xB3,0xBB): op(i,"conv.ovf.y",0)
op(0xC2,"refanyval","tok");op(0xC3,"ckfinite",0);op(0xC6,"mkrefany","tok");op(0xD0,"ldtoken","tok")
for i in range(0xD1,0xDD): op(i,"conv/arith",0)
op(0xD3,"conv.i",0);op(0xDC,"endfinally",0);op(0xDD,"leave",4);op(0xDE,"leave.s",1);op(0xDF,"stind.i",0);op(0xE0,"conv.u",0)
FE = {0x00:("arglist",0),0x01:("ceq",0),0x02:("cgt",0),0x03:("cgt.un",0),0x04:("clt",0),0x05:("clt.un",0),0x06:("ldftn","tok"),0x07:("ldvirtftn","tok"),0x09:("ldarg",2),0x0A:("ldarga",2),0x0B:("starg",2),0x0C:("ldloc",2),0x0D:("ldloca",2),0x0E:("stloc",2),0x0F:("localloc",0),0x11:("endfilter",0),0x12:("unaligned.",1),0x13:("volatile.",0),0x14:("tail.",0),0x15:("initobj","tok"),0x16:("constrained.","tok"),0x17:("cpblk",0),0x18:("initblk",0),0x1A:("rethrow",0),0x1C:("sizeof","tok"),0x1D:("refanytype",0),0x1E:("readonly.",0)}

CODED = {"TypeDefOrRef":(2,[2,1,0x1B]),"HasConstant":(2,[4,8,0x17]),"HasCustomAttribute":(5,[6,4,1,2,8,9,10,0,0x0E,0x17,0x14,0x11,0x1A,0x1B,0x20,0x23,0x26,0x27,0x28]),
 "HasFieldMarshal":(1,[4,8]),"HasDeclSecurity":(2,[2,6,0x20]),"MemberRefParent":(3,[2,1,0x1A,6,0x1B]),"HasSemantics":(1,[0x14,0x17]),"MethodDefOrRef":(1,[6,10]),
 "MemberForwarded":(1,[4,6]),"Implementation":(2,[0x26,0x23,0x27]),"CustomAttributeType":(3,[0,0,6,10,0]),"ResolutionScope":(2,[0,0x1A,0x23,1]),"TypeOrMethodDef":(1,[2,6])}
SCHEMA = {0x00:"2 S G G G",0x01:"c:ResolutionScope S S",0x02:"4 S S c:TypeDefOrRef t:04 t:06",0x03:"t:04",0x04:"2 S B",0x05:"t:06",0x06:"4 2 2 S B t:08",0x07:"t:08",0x08:"2 2 S",
 0x09:"t:02 c:TypeDefOrRef",0x0A:"c:MemberRefParent S B",0x0B:"2 c:HasConstant B",0x0C:"c:HasCustomAttribute c:CustomAttributeType B",0x0D:"c:HasFieldMarshal B",0x0E:"2 c:HasDeclSecurity B",
 0x0F:"2 4 t:02",0x10:"4 t:04",0x11:"B",0x12:"t:02 t:14",0x13:"t:14",0x14:"2 S c:TypeDefOrRef",0x15:"t:02 t:17",0x16:"t:17",0x17:"2 S B",0x18:"2 t:06 c:HasSemantics",
 0x19:"t:02 c:MethodDefOrRef c:MethodDefOrRef",0x1A:"S",0x1B:"B",0x1C:"2 c:MemberForwarded S t:1A",0x1D:"4 t:04",0x1E:"4 4",0x1F:"4",0x20:"4 2 2 2 2 4 B S S",0x21:"4 t:20",0x22:"4 4 4 t:20",
 0x23:"2 2 2 2 4 B S S B",0x24:"4 t:23",0x25:"4 4 4 t:23",0x26:"4 S B",0x27:"4 4 S S c:Implementation",0x28:"4 4 S c:Implementation",0x29:"t:02 t:02",0x2A:"2 2 c:TypeOrMethodDef S",0x2B:"c:MethodDefOrRef B",0x2C:"t:2A c:TypeDefOrRef"}

class Module:
    def __init__(self, path):
        d = self.d = open(path, "rb").read()
        pe = struct.unpack_from("<I", d, 0x3C)[0]
        nsec = struct.unpack_from("<H", d, pe + 6)[0]; osz = struct.unpack_from("<H", d, pe + 20)[0]; opt = pe + 24
        self.secs = [struct.unpack_from("<IIII", d, opt + osz + i * 40 + 8) for i in range(nsec)]
        clr = self.off(struct.unpack_from("<I", d, opt + 96 + 14 * 8)[0])
        meta = self.off(struct.unpack_from("<I", d, clr + 8)[0])
        vlen = struct.unpack_from("<I", d, meta + 12)[0]; p = meta + 16 + ((vlen + 3) & ~3)
        n = struct.unpack_from("<H", d, p + 2)[0]; p += 4; st = {}
        for _ in range(n):
            o, s = struct.unpack_from("<II", d, p); e = d.index(b"\0", p + 8); st[d[p + 8:e].decode()] = meta + o; p = (e + 4) & ~3
        self.strings, self.us, self.blob = st["#Strings"], st.get("#US"), st["#Blob"]
        t = st.get("#~", st.get("#-")); heap = d[t + 6]; valid = struct.unpack_from("<Q", d, t + 8)[0]
        self.ss = 4 if heap & 1 else 2; self.gs = 4 if heap & 2 else 2; self.bs = 4 if heap & 4 else 2
        cur = t + 24; self.rows = {}
        for i in range(64):
            if valid >> i & 1: self.rows[i] = struct.unpack_from("<I", d, cur)[0]; cur += 4
        self.tables = {}
        for i in range(0x2D):
            if i not in self.rows: continue
            cols = self.cols(SCHEMA[i]); size = sum(c[1] for c in cols)
            self.tables[i] = (cur, size, cols); cur += size * self.rows[i]
    def off(self, rva):
        for vs, va, rs, raw in self.secs:
            if va <= rva < va + max(vs, rs): return rva - va + raw
    def idx(self, t): return 4 if self.rows.get(t, 0) >= 0x10000 else 2
    def cidx(self, name):
        bits, ts = CODED[name]; return 4 if max(self.rows.get(x, 0) for x in ts) >= (1 << (16 - bits)) else 2
    def cols(self, spec):
        out = []
        for c in spec.split():
            if c in ("2", "4"): out.append((c, int(c)))
            elif c == "S": out.append((c, self.ss))
            elif c == "G": out.append((c, self.gs))
            elif c == "B": out.append((c, self.bs))
            elif c.startswith("t:"): out.append((c, self.idx(int(c[2:], 16))))
            else: out.append((c, self.cidx(c[2:])))
        return out
    def row(self, t, i):
        start, size, cols = self.tables[t]; p = start + (i - 1) * size; vals = []
        for kind, sz in cols:
            vals.append(int.from_bytes(self.d[p:p + sz], "little")); p += sz
        return vals
    def s(self, i):
        e = self.d.index(b"\0", self.strings + i); return self.d[self.strings + i:e].decode("utf8", "replace")
    def typedefs(self):
        nest = {}
        for i in range(1, self.rows.get(0x29, 0) + 1):
            c, e = self.row(0x29, i); nest[c] = e
        names = {}
        for i in range(1, self.rows[2] + 1):
            r = self.row(2, i); names[i] = (self.s(r[2]) + "." if r[2] else "") + self.s(r[1])
        full = {}
        for i in names:
            n, j = names[i], i
            while j in nest: j = nest[j]; n = names[j] + "/" + n
            full[i] = n
        return full
    def methods_of(self, ti):
        n = self.rows[2]; r = self.row(2, ti); first = r[5]
        last = self.row(2, ti + 1)[5] if ti < n else self.rows[6] + 1
        return [(m, self.s(self.row(6, m)[3])) for m in range(first, last)]
    def fields_of(self, ti):
        n = self.rows[2]; first = self.row(2, ti)[4]
        last = self.row(2, ti + 1)[4] if ti < n else self.rows[4] + 1
        return [(f, self.s(self.row(4, f)[1])) for f in range(first, last)]
    def tokname(self, tok):
        t, i = tok >> 24, tok & 0xFFFFFF
        if t == 0x70:
            p = self.us + i; ln = self.d[p]; return repr(self.d[p + 1:p + ln].decode("utf-16le", "replace"))
        if t == 0x06: return self.ownername(6, i) + "::" + self.s(self.row(6, i)[3])
        if t == 0x04: return self.ownername(4, i) + "::" + self.s(self.row(4, i)[1])
        if t == 0x0A:
            par, nm, _ = self.row(0x0A, i); tag = par & 7; pi = par >> 3
            pn = {0: lambda: self.tdname(pi), 1: lambda: self.trname(pi)}.get(tag, lambda: "?%d" % tag)()
            return pn + "::" + self.s(nm)
        if t == 0x02: return self.tdname(i)
        if t == 0x01: return self.trname(i)
        return hex(tok)
    def tdname(self, i):
        if not hasattr(self, "_td"): self._td = self.typedefs()
        return self._td.get(i, "?")
    def trname(self, i):
        r = self.row(1, i); return (self.s(r[2]) + "." if r[2] else "") + self.s(r[1])
    def ownername(self, table, i):
        if not hasattr(self, "_own"):
            self._own = {}
            for ti in range(1, self.rows[2] + 1):
                for m, _ in self.methods_of(ti): self._own[(6, m)] = ti
                for f, _ in self.fields_of(ti): self._own[(4, f)] = ti
        return self.tdname(self._own.get((table, i), 0))
    def il(self, m):
        rva = self.row(6, m)[0]
        if not rva: return ["<no body>"]
        p = self.off(rva); h = self.d[p]
        if h & 3 == 2: size = h >> 2; p += 1
        else: size = struct.unpack_from("<I", self.d, p + 4)[0]; p += (struct.unpack_from("<H", self.d, p)[0] >> 12) * 4
        code = self.d[p:p + size]; out = []; i = 0
        while i < len(code):
            at = i; b = code[i]; i += 1
            if b == 0xFE: name, arg = FE[code[i]]; i += 1
            else: name, arg = OPS.get(b, ("?%02x" % b, 0))
            txt = ""
            if arg == "tok": tok = struct.unpack_from("<I", code, i)[0]; i += 4; txt = self.tokname(tok)
            elif arg == "r4": txt = str(struct.unpack_from("<f", code, i)[0]); i += 4
            elif arg == "sw": n = struct.unpack_from("<I", code, i)[0]; i += 4 + 4 * n
            elif arg in (1, 2, 4, 8):
                v = int.from_bytes(code[i:i + arg], "little", signed=True); i += arg
                txt = "-> %04x" % (i + v) if name.startswith(("b", "leave")) else str(v)
            out.append("%04x %s %s" % (at, name, txt))
        return out
