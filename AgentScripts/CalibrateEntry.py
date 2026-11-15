"""Banco numérico local para calibrar el guiado de alabeo; no modifica datos."""
import json, math
from pathlib import Path
f=json.loads(Path('Assets/LaunchPrototype/Resources/MissionData/fisica.json').read_text(encoding='utf-8'))
def rho(h):
    h=max(0,h)
    if h<11000:
        t=300.15-.0065*h
        return 101325*(t/300.15)**5.2561/(287.05*t)*(1-.015*math.exp(-h/2500))
    if h<25000:return 22632*math.exp(-f['g0']*(h-11000)/(287.05*216.65))/(287.05*216.65)
    return .04008*math.exp(-(h-25000)/6800)
def sim(target=2.6,horizon=60,response=15,gamma=-6):
    m=9350; y=[120000,11000,math.radians(gamma)]; maxg=0; peakheat=0; dt=.2
    def force(y):
        h,v,gam=y; v=max(1,v); drag=.5*rho(h)*v*v*math.pi*(5.02/2)**2*1.35
        return drag, drag*.3
    def deriv(y,sigma):
        h,v,gam=y; v=max(1,v); r=6371000+max(0,h); grav=f['mu_tierra']/r**2; drag,lift=force(y)
        return [v*math.sin(gam),-drag/m-grav*math.sin(gam),lift*math.cos(sigma)/(m*v)+(v/r-grav/v)*math.cos(gam)]
    for i in range(36000):
        h,v,gam=y; drag,lift=force(y); r=6371000+h; grav=f['mu_tierra']/r**2
        rt=target*m*f['g0']/(.5*v*v*math.pi*(5.02/2)**2*1.35*math.sqrt(1+.3**2))
        ht=25000-6800*math.log(rt/.04008)
        desired=max(math.radians(-80),min(math.radians(5),(ht-h)/(v*horizon)-13600*drag/(m*v*v)))
        cg=((desired-gam)/response-(v/r-grav/v)*math.cos(gam))*m*v/max(1,lift)
        sigma=math.acos(max(-1,min(1,cg)))
        if v>9000: sigma=math.pi if gam>0 and h>60000 else 0
        a=deriv(y,sigma); b=deriv([y[j]+a[j]*dt/2 for j in range(3)],sigma); c=deriv([y[j]+b[j]*dt/2 for j in range(3)],sigma); e=deriv([y[j]+c[j]*dt for j in range(3)],sigma)
        y=[y[j]+dt*(a[j]+2*b[j]+2*c[j]+e[j])/6 for j in range(3)]
        ng=math.hypot(drag,lift)/(m*f['g0']); maxg=max(maxg,ng)
        heat=1.7415e-4*math.sqrt(rho(h)/6)*v**3; peakheat=max(peakheat,heat)
        if y[0]<7500 or ng>4.5: return dict(target=target,horizon=horizon,response=response,t=i*dt,h=y[0],v=y[1],maxg=maxg,heat=peakheat)
    return dict(timeout=True,h=y[0],v=y[1],maxg=maxg)
if __name__=='__main__':
    for horizon in [30,60,90]:
        print(sim(horizon=horizon))
