namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Propiedades de Maestría de Armas (Weapon Mastery) introducidas formalmente en D&amp;D 5e (2024).
/// </summary>
public enum MasteryProperty
{
    /// <summary>
    /// Cleave (Hendedura): Al impactar a un objetivo con un arma pesada, puedes realizar un ataque adicional contra otra criatura adyacente.
    /// </summary>
    Cleave = 1,

    /// <summary>
    /// Graze (Rozadura): Si fallas la tirada de ataque contra una criatura, infliges daño igual al modificador de habilidad correspondiente.
    /// </summary>
    Graze = 2,

    /// <summary>
    /// Nick (Tajo Rápido): Permite realizar el ataque adicional con un arma ligera como parte de la acción de Ataque, sin consumir la Acción Adicional.
    /// </summary>
    Nick = 3,

    /// <summary>
    /// Push (Empujón): Al impactar a una criatura de tamaño Grande o menor, la empujas hasta 10 pies de distancia en línea recta.
    /// </summary>
    Push = 4,

    /// <summary>
    /// Sap (Contusión): Al impactar a una criatura, esta sufre desventaja en su próxima tirada de ataque antes del inicio de tu próximo turno.
    /// </summary>
    Sap = 5,

    /// <summary>
    /// Slow (Frenado): Al impactar e infligir daño, la velocidad de movimiento del objetivo se reduce en 10 pies hasta el inicio de tu próximo turno.
    /// </summary>
    Slow = 6,

    /// <summary>
    /// Topple (Derribo): Al impactar, el objetivo debe superar una salvación de Constitución (CD = 8 + Modificador + Bono de Competencia) o quedar Derribado (Prone).
    /// </summary>
    Topple = 7,

    /// <summary>
    /// Vex (Hostigamiento): Al impactar e infligir daño, obtienes ventaja en tu próxima tirada de ataque contra ese mismo objetivo antes del final de tu próximo turno.
    /// </summary>
    Vex = 8
}
