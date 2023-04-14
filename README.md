# NetworkSimulation-Unity-

![image](https://user-images.githubusercontent.com/56947207/232078554-ebb3c2a8-07a4-40a8-b9cf-1a4f350a868c.png)

Latency 상황에서 Client와 Server 간의 움직임 보간을 구현하는 시뮬레이션을 Unity Engine을 이용해 개발하였습니다.

Client-Server Game Architecture in Fast-Paced Multiplayer 라는 칼럼에서 소개한 보간 기법들을 적용하였습니다.

https://www.gabrielgambetta.com/client-server-game-architecture.html


주요 보간 기법
=============

Client-side Prediction
--------------
![image](https://user-images.githubusercontent.com/56947207/232088652-42cbadb9-9f3d-41c3-9233-883b193d5c54.png)

Client-Server Model 이 Authoritative server를 사용한다고 했을 때, 클라이언트의 이동 액션에 대한 좌표 처리는 서버에서 이루어지고 클라이언트는 이 결과값을 받아서 화면에 렌더링한다.

즉, 클라이언트와 서버 사이에 지연시간이 존재하는 경우 플레이어는 자신의 입력에 대해 즉각적인 반응을 화면상에서 얻지 못하게 되고 이는 플레이적 불편함으로 이어지게 된다.

![image](https://user-images.githubusercontent.com/56947207/232090843-f6328d0f-7e8b-47d7-85d5-871ac665d4b6.png)

하지만 게임 월드가 플레이어의 입력에 대해 충분히 Deterministic 하다면 클라이언트 측에서는 굳이 서버의 좌표 처리를 기다리지 않고 플레이어의 입력을 바로 처리해서 화면에 렌더링 할 수 있다.


Server reconciliation
-------------------

![image](https://user-images.githubusercontent.com/56947207/232091763-ba930abd-b1e3-48aa-8c39-88390002c8a9.png)

하지만 지연시간이 충분히 크다면 Client-side Prediction 만으로는 위와 같은 동기화 불일치 이슈가 발생한다. RTT = 250ms 이라고 했을 때, T = 250ms 에서 플레이어의 좌표는 Client-side Prediction에 의해 (12,10)이지만 이 때 서버가 처리해서 보내준 확정 좌표는 (11,10) 이다.

따라서 플레이어는 (11,10)으로 순간이동을 하고 100ms 후에 보내진 패킷에 의해 또 순간이동을 해서 이번에는 다시 (12,10)으로 돌아갈 것이다. 이러한 잦은 순간이동은 플레이어에게 불편함을 준다.

![image](https://user-images.githubusercontent.com/56947207/232093396-7a996899-e477-40f2-b704-78267014ef00.png)

이 때 사용되는 보간 기법이 Server reconciliation 이다. 클라이언트 측에서는 입력 요청들을 복사하여 발생 순서대로 저장한다(#1,#2,...). 그리고 서버는 좌표를 처리하고 클라이언트로 패킷을 보낼 때 마지막으로 처리된 요청의 번호(sequence number)를 붙여서 보낸다.

그러면 클라이언트 측에서는 서버가 마지막으로 처리한 입력(확정된 좌표)과 아직 처리되지 않은 입력이 있다는것을 sequence number를 통해 알 수 있다. 이를 바탕으로 현재 게임 상태를 예측하여 Client-side Prediction을 적용 한다. 코드로 구현한 이 기능은 다음과 같다.

```
//Receive the authoritative position of this client's entity
entity.delta_x = message.position;
if (server_reconciliation)
{
                    int j = 0;
                    while (j < pending_inputs.Count)
                    {
                        var input = pending_inputs[j];
                        if (input.input_sequence_number <= message.last_processed_input)
                        {
                            //Already processed. Just drop it.
                            pending_inputs.RemoveAt(j);
                        }
                        else
                        {
                            //Not processed. Re-apply it.
                            entity.ApplyInput(input);
                            j++;
                        }
                    }
}
```

Entity interpolation
--------------------

![image](https://user-images.githubusercontent.com/56947207/232097209-966fdbf6-0aba-490d-9d93-a0e15d9682c2.png)

위 두 보간 기법을 적용하면 서버의 Update rate 와 latency와는 상관없이 내가 조종하는 캐릭터에 한해서는 화면상에서 마치 싱글플레이를 하듯이 자연스러운 움직임을 볼 수 있다. 하지만 내 캐릭터를 보는 다른 플레이어의 화면은 어떨까?

위에서 진행되는 네트워크 시뮬레이션에서 서버는 다른 플레이어의 입력 정보를 전달(Dead reckoning)하지 않고 처리된 좌표 값을 전달한다. 따라서 서버의 Update rate가 충분하지 않다면 내 캐릭터는 다른 플레이어의 화면에 서버의 Update rate 주기로 뚝뚝 끊기면서 움직일 것이다.

![image](https://user-images.githubusercontent.com/56947207/232098758-74566cdb-bbfa-42b4-834f-d5f75267738a.png)

이 때 필요한 것이 Entity interpolation 이다. 서버로 부터 확정된 position 위치를 받아서 현재 위치와 확정된 위치 사이의 좌표를 보간해서 클라이언트의 매 프레임마다 update 해주는 것이다. 이 기법이 사용되려면 클라이언트와 서버간의 Timestamp가 동기화 되야 한다.

좌표 보간에는 다양한 방법이 사용 될 수 있다. 유니티 내부에서도 대표적인 Lerp()이외에도 다양한 보간 함수를 지원하고 있다. 
